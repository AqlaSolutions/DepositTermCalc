# DepositTermCalc
Helps managing your money. Calculates deposit terms required to always keep minimum amount of cash in your hands but no more than necessary.

## Overview
You need it if you want to keep most of your money in deposit bank accounts but need to withdraw some every month.
When you reopen deposits you need to know exact time when you will need it.
It's not so easy math if you have many of them.
This program solves it, just put current cash amount, withdrawal amount per month, deposit end dates in text file.
The algorithm generates a plan while trying to keep the specified amount of cash available at least once a month.
Though at other points of time it can be a bit less or more - banks open deposits for months, you can't just ask them to have a deposit for exact 128 days.
Another goal of the algorithm is to use a maximum possible term for each deposit to get maximum % (1 deposit for 6 months is better than 6 deposits for 3, 2, 1 months).
Deposit percents, tax and inflation are also taken into an account.

## In code
This is a simple tool and it's not meant to be maintained and developed for a long time. No OOP - plain algorithm. No tests - it's a debugger driven development =) 

## Usage
`DepositTermCalc.exe <input.txt>`

<a href="https://github.com/AqlaSolutions/DepositTermCalc/blob/master/input.txt">Example input file</a>.

## Demo
`<<` means withdrawing,
`>>` means opening a new deposit,
`vv` indicates a balance before next operation,
`^^` indicates a balance after next operation
<img src="https://i.imgur.com/OOlN1Y9.png" />

## <a href="https://github.com/AqlaSolutions/DepositTermCalc/releases">Download</a>
