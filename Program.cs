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
            public DateTime Start { get; }
            public decimal Balance { get; set; }

            public Period(DateTime start)
            {
                Start = start;
            }
        }

        static void Main(string[] args)
        {
            var lines = File.ReadAllLines(args[0]);
            var startWeek = DateTime.Parse(lines[0]).GetWeekStart();
            const int periodsPerMonth = 4;
            var diffPerWeek = decimal.Parse(lines[1]) / periodsPerMonth;
            var startingBalance = decimal.Parse(lines[2]);
            var maxWeeksPerDeposit = int.Parse(lines[3]) * periodsPerMonth;
            if (lines[4] != "") throw new ArgumentException();
            var oldDeposits = lines.Skip(5).Select(s => s.Split(new[] { ' ' }, 3))
                .Select(ss => new Deposit(decimal.Parse(ss[1])) { EndDate = DateTime.Parse(ss[0]), Comment = ss.Length >= 3 ? ss[2] : ""}).OrderBy(x => x.EndDate)
                .ToList();
            var oldDepositsByEndWeek = oldDeposits.GroupBy(x => x.EndDate.Value.GetWeekStart()).ToDictionary(x => x.Key, x => x.ToList());

            DateTime counterDt = startWeek;

            var periods = new List<Period>();
            for (int i = 0; i < 48 * periodsPerMonth; i++)
            {
                periods.Add(new Period(counterDt));
                var w = counterDt.Day / 7 + 1;
                counterDt = counterDt.AddDays(-counterDt.Day + 1);
                counterDt = w >= 4 ? counterDt.AddMonths(1) : counterDt.AddDays(7 * w);
            }


            foreach (var p in periods)
            {
                if (oldDepositsByEndWeek.TryGetValue(p.Start, out var d)) 
                    p.Balance += d.Sum(x => x.Amount);
            }

            periods[0].Balance += startingBalance;

            int newDepositsCounter = 0;
            decimal newDepositsBalance = 0;
            var notReturnedDeposits = new List<Deposit>();
            var returnedNewDeposits = new List<Deposit>();
            for (int i = 0; i < periods.Count; i++)
            {
                var period = periods[i];
                if (i >= maxWeeksPerDeposit)
                {
                    var minPeriod = periods[i - maxWeeksPerDeposit];
                    while (notReturnedDeposits.Count > 0 && notReturnedDeposits.Last().StartDate <= minPeriod.Start)
                    {
                        var d = notReturnedDeposits.Last();
                        notReturnedDeposits.RemoveAt(notReturnedDeposits.Count - 1);
                        d.EndDate = period.Start;
                        newDepositsBalance -= d.Amount;
                        period.Balance += d.Amount;
                        returnedNewDeposits.Add(d);
                    }
                }

                var overBalance = period.Balance + diffPerWeek;
                if (overBalance > 0)
                {
                    var deposit = new Deposit(overBalance) { StartDate = period.Start, Comment = "new#"+(++newDepositsCounter) };
                    notReturnedDeposits.Insert(0, deposit);
                    newDepositsBalance += overBalance;
                    period.Balance -= overBalance;
                }
                else if (overBalance < 0)
                {
                    decimal left = -overBalance;

                    while (left > 0 && newDepositsBalance > 0)
                    {
                        var deposit = notReturnedDeposits.Last();
                        bool tooShortPeriod = (period.Start - deposit.StartDate.Value).TotalDays < 30 - 7 / 2;
                        if (tooShortPeriod)
                        {
                            // can't be a real deposit so better use cash from most recent deposit instead
                            deposit = notReturnedDeposits.First();
                        }

                        decimal taken = Math.Min(left, deposit.Amount);
                        left -= taken;
                        period.Balance += taken;
                        if (taken == deposit.Amount)
                        {
                            notReturnedDeposits.Remove(deposit);
                        }
                        else
                        {
                            deposit.Amount -= taken;
                            var comment = deposit.Comment;
                            if (tooShortPeriod)
                                comment += "*";
                            else
                                deposit.Comment += "*";
                            deposit = new Deposit(taken) { StartDate = deposit.StartDate, Comment = comment };
                        }

                        deposit.EndDate = period.Start;

                        newDepositsBalance -= taken;

                        if (!tooShortPeriod) 
                            returnedNewDeposits.Add(deposit);
                        else
                        {
                            for (int j = i - 1; j >= 0 && deposit.StartDate < periods[j].Start; j--)
                            {
                                periods[j].Balance += deposit.Amount;
                            }
                        }
                    }
                }
            }

            foreach (var d in notReturnedDeposits)
            {
                d.EndDate = periods[periods.Count - 1].Start;
                returnedNewDeposits.Add(d);
            }

            notReturnedDeposits.Clear();
            newDepositsBalance = 0;

            foreach (var p in periods)
            {
                Console.WriteLine($"{p.Start:yyyy-MM-dd} till {p.Start.AddDays(6):MM-dd} {p.Balance}");
                bool any = false;
                foreach (var d in returnedNewDeposits.Where(x => x.EndDate == p.Start))
                {
                    Console.WriteLine($"   <<new deposit {d.Amount} from {d.StartDate.Value.Date:yyyy-MM-dd} ({GetCelanderMonthDiff(d.EndDate.Value, d.StartDate.Value)}m)");
                    any = true;
                }

                if (oldDepositsByEndWeek.TryGetValue(p.Start, out var list))
                {
                    foreach (var d in list)
                    {
                        Console.WriteLine($"   <<old deposit {d.Amount} `{d.Comment}`");
                        any = true;
                    }
                }

                foreach (var d in returnedNewDeposits.Where(x => x.StartDate == p.Start))
                {
                    Console.WriteLine($"   >>new deposit {GetCelanderMonthDiff(d.EndDate.Value, d.StartDate.Value)}m {d.Amount} `{d.Comment}` till {d.EndDate:yyyy-MM-dd}");

                    bool first = true;
                    foreach (var r in oldDeposits.Concat(returnedNewDeposits).Where(x => x != d 
                        && x.EndDate >= d.EndDate.Value.AddDays(-15) 
                        && x.EndDate <= d.EndDate.Value
                        && x.StartDate <= d.StartDate)
                        .OrderBy(x => x.StartDate))
                    {
                        if (first)
                        {
                            Console.Write("      can be added to deposits: ");
                            first = false;
                        }
                        else Console.Write(", ");

                        Console.Write(r.StartDate != null
                            ? $"{GetCelanderMonthDiff(r.EndDate.Value, r.StartDate.Value)}m {r.Amount} `{r.Comment}` {r.StartDate:yyyy-MM-dd} till {r.EndDate.Value:yyyy-MM-dd}"
                            : $"{r.Amount} till {r.EndDate:yyyy-MM-dd}");
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

    static class DateTimeEx
    {
        public static DateTime GetWeekStart(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day / 7 * 7 + 1);
        }
    }
}