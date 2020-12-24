using System;
using System.Collections.Generic;
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
            public DateTime? StartDate { get; set; }
            public decimal Amount { get; set; }
            public string Comment { get; set; } = "";

            public Deposit(decimal amount)
            {
                Amount = amount;
            }
        }

        class Period
        {
            public DateTime Month { get; }
            public decimal Balance { get; set; }

            public Period(DateTime month)
            {
                Month = month;
            }
        }

        static void Main(string[] args)
        {
            var lines = File.ReadAllLines(args[0]);
            var startMonth = DateTime.Parse(lines[0]);
            startMonth = startMonth.AddDays(-startMonth.Day + 1);
            var diffPerMonth = decimal.Parse(lines[1]);
            var startingBalance = decimal.Parse(lines[2]);
            var maxMonthPerDeposit = int.Parse(lines[3]);
            if (lines[4] != "") throw new ArgumentException();
            var oldDeposits = lines.Skip(5).Select(s => s.Split(new[] { ' ' }, 3))
                .Select(ss => new Deposit(decimal.Parse(ss[1])) { EndDate = DateTime.Parse(ss[0]), Comment = ss.Length >= 3 ? ss[2] : ""}).OrderBy(x => x.EndDate)
                .ToList();
            var oldDepositsByEndMonth = oldDeposits.GroupBy(x => x.EndDate.Value.AddDays(-x.EndDate.Value.Day + 1).AddMonths(x.EndDate.Value.Day > 15 ? 1 : 0))
                .ToDictionary(x => x.Key, x => x.ToList());

            var periods = Enumerable.Range(0, 48).Select(x => new Period(startMonth.AddMonths(x))).ToList();

            foreach (var p in periods)
            {
                if (oldDepositsByEndMonth.TryGetValue(p.Month, out var d)) 
                    p.Balance += d.Sum(x => x.Amount);
            }

            periods[0].Balance += startingBalance;

            int newDepositsCounter = 0;
            decimal newDepositsBalance = 0;
            var notReturnedDeposits = new Queue<Deposit>();
            var returnedNewDeposits = new List<Deposit>();
            for (int i = 0; i < periods.Count; i++)
            {
                var p = periods[i];
                if (i >= maxMonthPerDeposit)
                {
                    var minPeriod = periods[i - maxMonthPerDeposit];
                    while (notReturnedDeposits.Count > 0 && notReturnedDeposits.Peek().StartDate <= minPeriod.Month)
                    {
                        var d = notReturnedDeposits.Dequeue();
                        d.EndDate = p.Month;
                        newDepositsBalance -= d.Amount;
                        p.Balance += d.Amount;
                        returnedNewDeposits.Add(d);
                    }
                }

                var overBalance = p.Balance + diffPerMonth;
                if (overBalance > 0)
                {
                    var deposit = new Deposit(overBalance) { StartDate = p.Month, Comment = "new#"+(++newDepositsCounter) };
                    notReturnedDeposits.Enqueue(deposit);
                    newDepositsBalance += overBalance;
                    p.Balance -= overBalance;
                }
                else if (overBalance < 0)
                {
                    decimal left = -overBalance;
                    while (left > 0 && newDepositsBalance > 0)
                    {
                        var deposit = notReturnedDeposits.Peek();
                        decimal taken = Math.Min(left, deposit.Amount);
                        left -= taken;
                        p.Balance += taken;
                        if (taken == deposit.Amount)
                        {
                            notReturnedDeposits.Dequeue();
                        }
                        else
                        {
                            deposit.Amount -= taken;
                            var comment = deposit.Comment;
                            deposit.Comment += "*";
                            deposit = new Deposit(taken) { StartDate = deposit.StartDate, Comment = comment };
                        }

                        deposit.EndDate = p.Month;
                        returnedNewDeposits.Add(deposit);
                        newDepositsBalance -= taken;
                    }
                }
            }

            foreach (var d in notReturnedDeposits)
            {
                d.EndDate = periods[periods.Count - 1].Month;
                returnedNewDeposits.Add(d);
            }

            notReturnedDeposits.Clear();
            newDepositsBalance = 0;

            foreach (var p in periods)
            {
                Console.WriteLine($"{p.Month:yyyy MM} {p.Balance}");
                bool any = false;
                foreach (var d in returnedNewDeposits.Where(x => x.EndDate == p.Month))
                {
                    Console.WriteLine($"   <<new deposit {d.Amount} from {d.StartDate:yyyy MM} ({GetCelanderMonthDiff(d.EndDate.Value, d.StartDate.Value)}m)");
                    any = true;
                }

                if (oldDepositsByEndMonth.TryGetValue(p.Month, out var list))
                {
                    foreach (var d in list)
                    {
                        Console.WriteLine($"   <<old deposit {d.Amount} `{d.Comment}`");
                        any = true;
                    }
                }

                foreach (var d in returnedNewDeposits.Where(x => x.StartDate == p.Month))
                {
                    Console.WriteLine($"   >>new deposit {GetCelanderMonthDiff(d.EndDate.Value, d.StartDate.Value)}m {d.Amount} `{d.Comment}` till {d.EndDate:yyyy MM}");

                    bool first = true;
                    foreach (var r in oldDeposits.Concat(returnedNewDeposits).Where(x => x != d && x.EndDate == d.EndDate && x.StartDate <= d.StartDate).OrderBy(x => x.StartDate))
                    {
                        if (first)
                        {
                            Console.Write("      can be added to deposits: ");
                            first = false;
                        }
                        else Console.Write(", ");

                        Console.Write(r.StartDate != null
                            ? $"{GetCelanderMonthDiff(r.EndDate.Value, r.StartDate.Value)}m {r.Amount} `{r.Comment}` {r.StartDate:yyyy MM} till {r.EndDate.Value:yyyy MM}"
                            : $"{r.Amount} till {r.EndDate:yyyy MM}");
                    }

                    if (!first) Console.WriteLine();


                    any = true;
                }

                if (any) Console.WriteLine();
            }

            Console.ReadLine();
        }

        static int GetCelanderMonthDiff(DateTime date1, DateTime date2)
        {
            return (((date1.Year - date2.Year) * 12) + date1.Month) - date2.Month;
        }
    }
}