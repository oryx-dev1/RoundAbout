![Build](https://github.com/oryx-dev1/RoundAbout/actions/workflows/dotnet.yml/badge.svg)
# Round About

A command-line savings app written in C#. Every payment is **rounded up to the next whole OMR**, and the spare change is saved automatically.

> Pay **1.200 OMR** at a shop → **2.000 OMR** leaves your balance → **0.800 OMR** goes into savings.

## Features

- **Round-up payments**: spare change is saved on every purchase
- **Payment history**: date, shop, amount, rounded charge and amount saved, with shop-name filtering
- **Summary**: total spent, average and largest payment, and your top shop
- **Savings goal**: set a target and track it with a progress bar
- **Deposit**: add money to your balance
- **Undo**: reverse the last payment, including its savings
- **Persistent data**: everything is stored in `roundabout.json` between sessions
- **Input validation**: bad amounts, empty shop names and overspending are rejected

## Getting Started

**Requirements:** [.NET 10 SDK](https://dotnet.microsoft.com/download) or newer

```bash
git clone https://github.com/oryx-dev1/RoundAbout.git
cd RoundAbout
dotnet restore
dotnet run --project RoundAbout/RoundAbout.csproj
dotnet test
```

## Usage

Choose menu options by number or by name (e.g. `1` or `pay`).

```
╔════════════════════════════════╗
║        ROUND ABOUT  (OMR)      ║
╚════════════════════════════════╝
  Balance: 1000.000 OMR   Saved: 0.000 OMR

  1. Pay            5. Summary
  2. History        6. Deposit
  3. Balance        7. Savings goal
  4. Savings        8. Undo last payment
  0. Exit
```

### Making a payment

Enter the amount and shop name separated by a comma:

```
Enter amount in OMR and shop name (e.g. 1.2, Target):
1.2, Target

[Success] Paid 1.200 OMR at Target
  Rounded up to : 2.000 OMR
  Saved         : 0.800 OMR
  New balance   : 998.000 OMR
  Total savings : 0.800 OMR
  Goal: [████░░░░░░░░░░░░░░░░] 20% of 4.000 OMR
```

### Viewing history

```
  #  Date          Shop                Amount   Charged    Saved
──────────────────────────────────────────────────────────────────
  1  08 Nov 14:32 Target                1.200     2.000    0.800
  2  08 Nov 15:10 Carrefour            12.350    13.000    0.650
──────────────────────────────────────────────────────────────────
```

## How It Works

```
charged = ceil(amount)
saved   = charged - amount
balance -= charged
savings += saved
```

Amounts that are already whole numbers (e.g. `5`) save nothing.

## Project Structure

| File | Purpose |
|------|---------|
| `Program.cs` | Console menu and app flow (pay/history/summary/deposit/goal/undo) |
| `Logic.cs` | Reusable calculation and formatting helpers for payment logic |
| `RoundAbout.Tests/RoundAboutLogicTests.cs` | Unit tests for core payment and parsing behavior |
| `roundabout.json` | Created on first payment; stores balance, savings, goal and history |

To reset the app, delete `roundabout.json`.

## Roadmap

- [ ] Multiple accounts
- [ ] Export history to CSV
- [ ] Configurable rounding (nearest 0.5 or 0.100 OMR)
- [ ] Monthly savings reports
- [ ] Expand unit test coverage

## License

CC0 free for all!
