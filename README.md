# DepositTermCalc
Helps managing your money. Calculates deposit terms required to always keep minimum amount of cash in your hands but no more than necessary. Doesn't predict added percents so you might need to re-run this a few times per year.

## Motivation
You need it if you want to keep most of your money in deposit bank accounts but need to withdraw some every month.
When you reopen deposits you need to know exact time when you will need it.
It's not so easy math if you have many of them.
This program solves it, just put current cash amount, withdrawal amount per month, deposit end dates in text file.
The algorithm generates a plan while trying to keep the specified amount of cash available at least once of every month.
Though at other points of time it can be 1/2 less or more - banks open deposits for months, you can't just ask them to have a deposit for exact 128 days.
Another goal in mind is to use the maximum possible term for each deposit to get maximum %.

## Usage
`DepositTermCalc.exe <input.txt>`

Example input file: input.txt in sources.

The parser doesn't support comments so you need to remove ` -- stuff` from the input file before launching.

## Demo
`<<` means withdrawing,
`>>` means opening a new deposit

<img src="https://i.imgur.com/0MPwAwi.png" />

## <a href="https://github.com/AqlaSolutions/DepositTermCalc/releases">Download</a>
