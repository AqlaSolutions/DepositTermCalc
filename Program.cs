using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
            public bool IsHoldInCash { get; set; }
            public decimal Amount { get; set; }
            public string Comment { get; set; } = "";
            
            public Deposit(decimal amount)
            {
                Amount = amount;
            }
        }


        static void Main(string[] args)
        {
            string PreprocessForParsing(string line)
            {
                int index = line.IndexOf(" --");
                if (index != -1)
                    line = line.Substring(0, index);
                else if (line.StartsWith("--")) line = "";
                return line;
            }

            decimal ParsePercent(string s)
            {
                s = s.Trim();
                Trace.Assert(s.EndsWith("%"));
                s = s.Substring(0, s.Length - 1);
                return decimal.Parse(s);
            }

            // for unified decimal numbers parsing
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            var dtCulture = CultureInfo.GetCultureInfo("ru-RU");

            var lines = File.ReadAllLines(args[0]).Select(PreprocessForParsing).ToList();
            var startDt = DateTime.Parse(lines[0], dtCulture);
            var diffPerMonth = decimal.Parse(lines[1]);
            var startingBalance = decimal.Parse(lines[2]);
            var maxDepositDurationMonths = int.Parse(lines[3]);
            var taxPercent = ParsePercent(lines[5]);
            var annualInflationPercent = ParsePercent(lines[6]);
            var depositPercents = lines[4].Split(new[]{' ' }, StringSplitOptions.RemoveEmptyEntries).Select(ParsePercent)
                .Select(x =>
                    (100m + x * (1m - taxPercent / 100m)) * (1m - annualInflationPercent / 100m) - 100m).ToList();
            
            decimal GetAmountWithPercent(Deposit deposit, DateTime? endDate = null)
            {
                endDate = endDate ?? deposit.EndDate;
                if (deposit.StartDate == null || endDate == null || depositPercents.Count == 0) 
                    return deposit.Amount;

                var months = GetDepositMonthDiff(endDate.Value, deposit.StartDate.Value);
                var annualPercent = depositPercents[Math.Min(months, depositPercents.Count - 1)];

                return deposit.Amount * (1m + annualPercent / 100m / 365m * (decimal)(endDate - deposit.StartDate).Value.TotalDays);
            }

            decimal WithdrawalToInitialAmount(Deposit deposit, decimal amount, DateTime? endDate = null)
            {
                endDate = endDate ?? deposit.EndDate;
                if (deposit.StartDate == null || endDate == null || depositPercents.Count == 0)
                    return amount;
                
                var months = GetDepositMonthDiff(endDate.Value, deposit.StartDate.Value);
                var annualPercent = depositPercents[Math.Min(months, depositPercents.Count - 1)];

                return amount / (1m + annualPercent / 100m / 365m * (decimal)(endDate - deposit.StartDate).Value.TotalDays);
            }

            if (lines[7] != "") throw new ArgumentException();

            // output in usual format
            CultureInfo.DefaultThreadCurrentCulture = dtCulture;
            CultureInfo.DefaultThreadCurrentUICulture = dtCulture;
            CultureInfo.CurrentCulture = dtCulture;
            CultureInfo.CurrentUICulture = dtCulture;


            var oldDeposits = lines.Skip(8).Select(s => s.Split(new[] { ' ' }, 3))
                .Select(ss => new Deposit(decimal.Parse(ss[1])) { EndDate = CorrectIfWeekend(DateTime.Parse(ss[0], dtCulture)), Comment = ss.Length >= 3 ? ss[2] : ""}).OrderBy(x => x.EndDate)
                .ToList();

            decimal newDepositsBalance = 0;
            
            var notReturnedDeposits = new List<Deposit>();
            var returnedNewDeposits = new List<Deposit>();

            decimal balance = startingBalance;
            DateTime dt = startDt;
            
            var oldDepositsSet = oldDeposits.ToHashSet(); // remove deposits while processing

            int newDepositsCounter = 0;

            DateTime CorrectIfWeekend(DateTime dt)
            {
                return dt.DayOfWeek switch
                { 
                    DayOfWeek.Saturday => dt.AddDays(2),
                    DayOfWeek.Sunday => dt.AddDays(1),
                    _ => dt
                };
            }


            decimal InflatedDiffPerMonth() => 
                diffPerMonth > 0 
                ? diffPerMonth
                : diffPerMonth * (annualInflationPercent / 100m / 365m * (decimal)(dt - startDt).TotalDays + 1m);

            void DepositIfOverbalance()
            {
                var overBalance = balance + InflatedDiffPerMonth();
                if (overBalance > -InflatedDiffPerMonth() / 10m) // must keep at least 1 month of cash
                {
                    notReturnedDeposits.Add(new Deposit(overBalance) { StartDate = dt, Comment = "new#" + (++newDepositsCounter) });
                    newDepositsBalance += overBalance;
                    balance -= overBalance;
                }
            }
            
            DepositIfOverbalance();

            while (dt < startDt.AddYears(5))
            {
                var nextDeposit = oldDepositsSet.Concat(notReturnedDeposits)
                    .Select(deposit => (deposit, end:(DateTime?)CorrectIfWeekend(deposit.EndDate ?? deposit.StartDate.Value.AddDays(30 * maxDepositDurationMonths))))
                    .OrderBy(x=>x.end) // TODO MinBy
                    .FirstOrDefault();
                var withdrawalMaxDt = CorrectIfWeekend(dt.AddDays(Math.Max(1.0, (double) ((balance + InflatedDiffPerMonth() / 2) / (-InflatedDiffPerMonth() / 30m)))));
                if (nextDeposit.end < withdrawalMaxDt || (nextDeposit.end - withdrawalMaxDt)?.TotalDays <= 7)
                {
                    balance += InflatedDiffPerMonth() / 30m * (decimal) (nextDeposit.end.Value - dt).TotalDays;
                    dt = nextDeposit.end.Value;

                    var amount = nextDeposit.deposit.Amount;
                    if (nextDeposit.deposit.EndDate == null)
                    {
                        // this is our new deposit, apply percents
                        amount = GetAmountWithPercent(nextDeposit.deposit, dt);
                        // we keep only initial amount in this balance (no percents taken into account)
                        newDepositsBalance -= nextDeposit.deposit.Amount;
                        notReturnedDeposits.Remove(nextDeposit.deposit);
                        nextDeposit.deposit.EndDate = dt;
                        returnedNewDeposits.Add(nextDeposit.deposit);
                    }
                    else oldDepositsSet.Remove(nextDeposit.deposit);
                    
                    balance += amount;
                    DepositIfOverbalance();
                }
                else
                {
                    balance += InflatedDiffPerMonth() / 30m * (decimal) (withdrawalMaxDt - dt).TotalDays;
                    var overBalance = balance + InflatedDiffPerMonth();
                    dt = withdrawalMaxDt;

                    // it can't be >= 0 because all previous overbalance we already put into deposits
                    // and some more time passed after than
                    Trace.Assert(overBalance <= 0);
                    decimal left = -overBalance;

                    while (left > 0 && newDepositsBalance > 0.001m)
                    {
                        var deposit = notReturnedDeposits.First();
                        DateTime withdrawalDate;
                        {
                            int depositDays = (int)(dt - deposit.StartDate.Value).TotalDays;
                            // try to add more days
                            int ceil = (depositDays / 30 + 1) * 30;
                            int floor = depositDays / 30 * 30;

                            depositDays = ceil - depositDays <= depositDays - floor && ceil - depositDays <= 10 ? ceil : floor;

                            withdrawalDate = CorrectIfWeekend(deposit.StartDate.Value.AddDays(depositDays));
                        }


                        bool isHoldInCash = (withdrawalDate - deposit.StartDate.Value).TotalDays < 30;
                        decimal depositAmount;
                        if (isHoldInCash)
                        {
                            // can't be a real deposit so better use cash from most recent deposit instead
                            Trace.Assert(notReturnedDeposits.First().StartDate >= deposit.StartDate);
                            deposit = notReturnedDeposits.Last();
                            withdrawalDate = dt;
                            depositAmount = deposit.Amount;
                        }
                        else depositAmount = GetAmountWithPercent(deposit, withdrawalDate);

                        decimal taken = Math.Min(left, depositAmount);

                        left -= taken;
                        balance += taken;

                        var takenWithoutPercent = !isHoldInCash ? WithdrawalToInitialAmount(deposit, taken, withdrawalDate) : taken;
                        newDepositsBalance -= takenWithoutPercent;

                        if (taken == depositAmount)
                        {
                            notReturnedDeposits.Remove(deposit);
                        }
                        else
                        {
                            deposit.Amount -= takenWithoutPercent;
                            deposit = new Deposit(takenWithoutPercent) { StartDate = deposit.StartDate, Comment = "new#" + (++newDepositsCounter) };                            
                        }
                        

                        deposit.EndDate = withdrawalDate;
                        deposit.WantedEndDate = dt;
                        deposit.IsHoldInCash = isHoldInCash;

                        var dd = returnedNewDeposits.FirstOrDefault(x => x.StartDate == deposit.StartDate && x.EndDate == deposit.EndDate);
                        if (dd != null)
                            dd.Amount += deposit.Amount;
                        else
                            returnedNewDeposits.Add(deposit);                        
                    }

                    if (left > 0)
                    {
                        if (nextDeposit.deposit == null)
                        {
                            Console.WriteLine($"Enough money till {dt.AddDays((double) (balance / (-InflatedDiffPerMonth() / 30m))):d}");
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
                            balance += InflatedDiffPerMonth() / 30m * (decimal) (nextDt - dt).TotalDays;
                            if (balance < 0)
                                Console.WriteLine($"Gap from {dt:d} till {nextDt:d}: {balance:F0}");
                            dt = nextDt;
                        }

                    }
                }
            }

            Debug.Assert(Math.Abs(newDepositsBalance) <= 0.001m);

            Console.WriteLine("Simulation with fake dates!");
            OutputSimulation(true);
            Console.WriteLine();
            Console.WriteLine("----------------------------------------------");
            Console.WriteLine();
            Console.WriteLine("Simulation with actual dates!");
            OutputSimulation(false);

            void OutputSimulation(bool useWantedEndDate)
            {
                balance = startingBalance;
                dt = startDt;

                var events = oldDeposits
                    .Where(x => x.StartDate != null)
                    .Concat(returnedNewDeposits)
                    .Select(d => (start: true, deposit: d, dt: d.StartDate.Value))
                    .Concat(oldDeposits.Concat(returnedNewDeposits).Select(d => (start: false, deposit: d, dt: (useWantedEndDate ? d.WantedEndDate ?? d.EndDate : d.EndDate).Value)))
                    .OrderBy(x => x.dt)
                    .ThenBy(x => x.start ? 1 : 0)
                    .ToList();
            
                int eventIndex;
                for (eventIndex = 0; eventIndex < events.Count; eventIndex++)
                {
                    var ev = events[eventIndex];
                    var d = ev.deposit;
                    bool isContinuation = eventIndex > 0 && events[eventIndex - 1].dt == ev.dt;
                    bool isContinued = eventIndex + 1 < events.Count && events[eventIndex+1].dt==ev.dt;


                    var newDt = ev.dt;
                    balance += InflatedDiffPerMonth() / 30m * (decimal) (newDt - dt).TotalDays;
                    dt = newDt;
                    string dateSpaces = new string(' ', $"{dt:d}".Length);
                    string DateOrEmpty()
                    {
                        if (eventIndex <= 0 || dt != events[eventIndex - 1].dt) 
                            return $"{dt:d}";
                        else
                            return dateSpaces;
                    
                    }

                    Console.ForegroundColor = !isContinuation && balance < -InflatedDiffPerMonth() / 8 ? ConsoleColor.Red : ConsoleColor.DarkGray;
                
                    Console.WriteLine($"{dateSpaces} vv {balance:F0} vv");
                    Console.ForegroundColor = ConsoleColor.Gray;

                    if (ev.start)
                    {
                        balance -= d.Amount;
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Trace.Assert(d.StartDate==dt);
                        Console.Write($"{DateOrEmpty()} >> {d.Amount:F0} `{d.Comment}`");
                        if (d.IsHoldInCash) 
                            Console.Write($" [hold in cash {(d.EndDate-d.StartDate).Value.TotalDays} days]");
                        else
                            Console.Write($" {GetDepositMonthDiff(d.EndDate.Value, d.StartDate.Value)}m");
                        Console.WriteLine();
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
                                Console.Write($"{dateSpaces}      can be added to deposits: ");
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
                    
                        Trace.Assert(useWantedEndDate || d.EndDate == dt);
                        Console.Write($"{DateOrEmpty()} << {d.Amount:F0} `{d.Comment}`");
                        if (d.StartDate != null) Console.Write($" from {d.StartDate:d}");
                        if (d.IsHoldInCash)
                            Console.Write($" [hold in cash {(d.EndDate - d.StartDate).Value.TotalDays} days]");
                        else if (d.StartDate != null)
                            Console.Write($" ({GetDepositMonthDiff(d.EndDate.Value, d.StartDate.Value)}m)");
                        if (d.WantedEndDate != null && d.WantedEndDate != d.EndDate)
                        {
                            if (useWantedEndDate)
                                Console.Write($", actual end {d.EndDate:d}");
                            else
                                Console.Write($", wanted end {d.WantedEndDate:d}");
                        }
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                    decimal redThreshold = 0.3m;
                    if (useWantedEndDate) redThreshold = 0.1m;
                    Console.ForegroundColor = 
                        !isContinued 
                        && (balance < -InflatedDiffPerMonth() * (1m - redThreshold) 
                        || balance > -InflatedDiffPerMonth() * (1m + redThreshold * 2m)) 
                            ? ConsoleColor.Red 
                            : ConsoleColor.DarkGray;
                    Console.WriteLine($"{dateSpaces} ^^ {balance:F0} ^^");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    if (!isContinued) Console.WriteLine();
                }
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