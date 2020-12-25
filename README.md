# DepositTermCalc
Helps managing your money. Calculates deposit terms required to always keep minimum amount of cash in your hands but no more than necessary.

<img src="https://i.imgur.com/OOlN1Y9.png" />

## Overview
You might want it if you wish to keep most of your money in deposit bank accounts but need to withdraw some every month.
In this case when you (re)open deposits you have to know exact time when you need them back.
It's not so easy math if you have many of them.
The algorithm generates a plan while trying to keep the specified amount of cash available at least once a month.
Though at other points of time it can be a bit less or more - banks open deposits for months, you can't just ask them to have a deposit for exact 128 days.

## Features
* Colored deposit-withdrawal plan
* The plan ensures that you have specified amount of money each month 
* Highlights balances when they are noticiable bigger or less than the specified amount
* Shows when it can't avoid deposit durations of less than month (in such rare case you have to just keep the cash)
* Calculates how much time you have until you are out of money
* Tries to keep the longest possible deposit terms for maximum percents
* Detects when you can add money to existing deposits
* Tries to minimize amount of unncessary operations
* Annual percents
* Tax
* Inflation
* Maximum deposit duration can be limited

## Code
This is a simple tool and it's not meant to be maintained and developed for a long time. No OOP - plain algorithm. No tests - it's a debugger driven development =) 


## Usage
`DepositTermCalc.exe <input.txt>`

<a href="https://github.com/AqlaSolutions/DepositTermCalc/blob/master/input.txt">Example input file</a>.

Dates are specified as dddd.mm.yy though other formats might still work.

## Legend
`<<` means withdrawing,
`>>` means opening a new deposit,
`vv` indicates a balance before next operation,
`^^` indicates a balance after next operation

## License
Free for personal use. You can modify code but you are not allowed to distribute your changes in any form.

## <a href="https://github.com/AqlaSolutions/DepositTermCalc/releases">Download</a>
