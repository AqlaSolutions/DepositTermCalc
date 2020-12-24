using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace DepositTermCalc
{
    internal class Program
    {
        class Deposit
        {
            public DateTime? EndDate { get; set; }
            public DateTime? WantedEndDate { get; set; }
            public DateTime? StartDate { get; set; }
            public decimal Amount { get; set; }
            public string Comment { get; set; } = "";

            public Deposit(decimal amount)
            {
                Amount = amount;
            }
        }

        static void Main(string[] args)
        {
            var lines = File.ReadAllLines(args[0]);
            var startDt = DateTime.Parse(lines[0]);
            var diffPerMonth = decimal.Parse(lines[1]);
            var startingBalance = decimal.Parse(lines[2]);
            var maxDepositDurationMonths = int.Parse(lines[3]);
            if (lines[4] != "") throw new ArgumentException();
            var oldDeposits = lines.Skip(5).Select(s => s.Split(new[] { ' ' }, 3))
                .Select(ss => new Deposit(decimal.Parse(ss[1])) { EndDate = DateTime.Parse(ss[0]), Comment = ss.Length >= 3 ? ss[2] : ""}).OrderBy(x => x.EndDate)
                .ToList();

            decimal newDepositsBalance = 0;
            
            var notReturnedDeposits = new List<Deposit>();
            var returnedNewDeposits = new List<Deposit>();

            decimal balance = startingBalance;
            DateTime dt = startDt;

            var oldDepositsSet = oldDeposits.ToHashSet(); // remove deposits while processing

            int newDepositsCounter = 0;

            while (dt < startDt.AddYears(5))
            {
                var nextDeposit = oldDepositsSet.Concat(notReturnedDeposits)
                    .Select(deposit => (deposit,end:(DateTime?)(deposit.EndDate ?? deposit.StartDate.Value.AddDays(30 * maxDepositDurationMonths))))
                    .OrderBy(x=>x.end) // TODO MinBy
                    .FirstOrDefault();
                var withdrawalMaxDt = dt.AddDays(Math.Max(1.0, (double) ((balance + diffPerMonth / 2) / (-diffPerMonth / 30m))));
                if (nextDeposit.end < withdrawalMaxDt)
                {
                    balance += diffPerMonth / 30m * (decimal) (nextDeposit.end.Value - dt).TotalDays;
                    dt = nextDeposit.end.Value;
                    if (nextDeposit.deposit.EndDate == null)
                    {
                        newDepositsBalance -= nextDeposit.deposit.Amount;
                        notReturnedDeposits.Remove(nextDeposit.deposit);
                        nextDeposit.deposit.EndDate = dt;
                        returnedNewDeposits.Add(nextDeposit.deposit);
                    }
                    else oldDepositsSet.Remove(nextDeposit.deposit);
                    

                    balance += nextDeposit.deposit.Amount;
                    var overBalance = balance + diffPerMonth;
                    if (overBalance > 0) // must keep at least 1 month of cash
                    {
                        notReturnedDeposits.Add(new Deposit(overBalance) { StartDate = dt, Comment = "new#"+(++newDepositsCounter) });
                        newDepositsBalance += overBalance;
                        balance -= overBalance;
                    }
                }
                else
                {
                    balance += diffPerMonth / 30m * (decimal) (withdrawalMaxDt - dt).TotalDays;
                    var overBalance = balance + diffPerMonth;
                    dt = withdrawalMaxDt;

                    // it can't be >= 0 because all previous overbalance we already put into deposits
                    // and some more time passed after than
                    Trace.Assert(overBalance <= 0);
                    decimal left = -overBalance;

                    while (left > 0 && newDepositsBalance > 0.001m)
                    {
                        var deposit = notReturnedDeposits.First();
                        int depositDays = (int) (dt - deposit.StartDate.Value).TotalDays;

                        {
                            // try to add more days
                            int ceil = (depositDays / 30 + 1) * 30;
                            int floor = depositDays / 30 * 30;

                            depositDays = ceil - depositDays <= depositDays - floor && ceil - depositDays <= 10 ? ceil : floor;
                        }

                        bool lessThanMonth = depositDays < 30;
                        if (lessThanMonth)
                        {
                            // can't be a real deposit so better use cash from most recent deposit instead
                            Trace.Assert(notReturnedDeposits.First().StartDate >= deposit.StartDate);
                            deposit = notReturnedDeposits.Last();
                        }

                        decimal taken = Math.Min(left, deposit.Amount);


                        if (lessThanMonth)
                            Console.WriteLine($"Less than month deposit {taken:F0} from {deposit.StartDate:d} till {dt:d}");

                        left -= taken;
                        balance += taken;
                        if (taken == deposit.Amount)
                        {
                            notReturnedDeposits.Remove(deposit);
                        }
                        else
                        {
                            deposit.Amount -= taken;
                            deposit = new Deposit(taken) { StartDate = deposit.StartDate, Comment = "new#" + (++newDepositsCounter) };
                        }


                        newDepositsBalance -= taken;

                        if (!lessThanMonth) // like we've never deposited it in the first place
                        {
                            deposit.EndDate = deposit.StartDate.Value.AddDays(depositDays);
                            deposit.WantedEndDate = dt;

                            var dd = returnedNewDeposits.FirstOrDefault(x => x.StartDate == deposit.StartDate && x.EndDate == deposit.EndDate);
                            if (dd != null)
                                dd.Amount += deposit.Amount;
                            else
                                returnedNewDeposits.Add(deposit);
                        }
                    }

                    if (left > 0)
                    {
                        if (nextDeposit.deposit == null)
                        {
                            Console.WriteLine($"Enough money till {dt.AddDays((double) (balance / (-diffPerMonth / 30m))):d}");
                            break;
                        }
                        else if (nextDeposit.deposit.EndDate < nextDeposit.end.Value)
                        {
                            if (balance < 0)
                                Console.WriteLine($"Gap from {dt:d} {balance:F0}");
                        }
                        else
                        {
                            var nextDt = nextDeposit.end.Value;
                            balance += diffPerMonth / 30m * (decimal) (nextDt - dt).TotalDays;
                            if (balance < 0)
                                Console.WriteLine($"Gap from {dt:d} till {nextDt:d}: {balance:F0}");
                            dt = nextDt;
                        }

                    }
                }
            }

            Debug.Assert(newDepositsBalance <= 0.001m);

            balance = startingBalance;
            dt = startDt;

            var events = oldDeposits
                .Where(x => x.StartDate != null)
                .Concat(returnedNewDeposits)
                .Select(d => (start: true, deposit: d, dt: d.StartDate.Value))
                .Concat(oldDeposits.Concat(returnedNewDeposits).Select(d => (start: false, deposit: d, dt: d.EndDate.Value)))
                .OrderBy(x => x.dt)
                .ThenBy(x => x.start ? 1 : 0)
                .ToList();
            
            foreach (var ev in events)
            {
                var d = ev.deposit;


                var newDt = (ev.start ? d.StartDate.Value : d.EndDate.Value);
                balance += diffPerMonth / 30m * (decimal) (newDt - dt).TotalDays;
                dt = newDt;

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($" vv {balance:F0} vv");
                Console.ForegroundColor = ConsoleColor.Gray;

                if (ev.start)
                {
                    balance -= d.Amount;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($">>{d.StartDate:d} {d.Amount:F0} `{d.Comment}` {GetDepositMonthDiff(d.EndDate.Value, d.StartDate.Value)}m");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    bool first = true;
                    foreach (var r in oldDeposits.Concat(returnedNewDeposits).Where(x => x != d
                            && x.EndDate >= d.EndDate.Value.AddDays(-7)
                            && x.EndDate <= d.EndDate.Value.AddDays(7)
                            && x.StartDate <= d.StartDate)
                        .OrderBy(x => x.StartDate))
                    {
                        if (first)
                        {
                            Console.Write("  can be added to deposits: ");
                            first = false;
                        }
                        else Console.Write(", ");

                        Console.Write(r.StartDate != null
                            ? $"`{r.Comment}` {GetDepositMonthDiff(r.EndDate.Value, r.StartDate.Value)}m {r.Amount:F0} {r.StartDate:d} till {r.EndDate:d}"
                            : $"{r.Amount:F0} till {r.EndDate:d}");
                    }

                    if (!first) Console.WriteLine();
                }
                else
                {
                    balance += d.Amount;
                    Console.ForegroundColor = oldDeposits.Contains(d) ? ConsoleColor.Cyan : ConsoleColor.Green;
                    Console.Write($"<<{d.EndDate:d} {d.Amount:F0} `{d.Comment}`");
                    if (d.StartDate != null) Console.Write($" from {d.StartDate:d} ({GetDepositMonthDiff(d.EndDate.Value, d.StartDate.Value)}m)");
                    if (d.WantedEndDate != null && d.WantedEndDate != d.EndDate)
                        Console.Write($", wanted end {d.WantedEndDate:d}");
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Gray;
                }

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($" ^^ {balance:F0} ^^");
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            Console.WriteLine();
            Console.WriteLine("Periods check: ");
            int eventIndex = 0;
            dt = startDt;
            balance = startingBalance;
            for (int i = 0; i < 30 * 12 * 10 * 2; i++)
            {
                var nextDt = i % 2 == 0 ? dt.AddDays(15) : dt.AddDays(-dt.Day + 1).AddMonths(1);
                balance += diffPerMonth / 30m * (decimal)(nextDt - dt).TotalDays;

                for (; eventIndex < events.Count && events[eventIndex].dt <= nextDt; eventIndex++)
                {
                    var ev = events[eventIndex];
                    balance += ev.deposit.Amount * (ev.start ? -1 : 1);
                    if (balance < 0)
                    {
                        Console.WriteLine($"{dt:d} {balance:F0} ! intermediate negative balance");
                    }
                }


                Console.WriteLine($"{dt:d} {balance:F0}");
                if (eventIndex >= events.Count) break;
                dt = nextDt;
            }

            Console.ReadLine();
        }

        static int GetDepositMonthDiff(DateTime date1, DateTime date2)
        {
            double totalDays = (date1 - date2).TotalDays;
            var r = (int) (totalDays / 30.0);
            if (totalDays >= 28)
                return Math.Max(r, GetCelanderMonthDiff2(date1, date2));
            return r;
        }
        
        static int GetCelanderMonthDiff2(DateTime date1, DateTime date2)
        {
            return (((date1.Year - date2.Year) * 12) + date1.Month) - date2.Month;
        }

    }
}