# POS (Point of Sale)

**Description**
A POS System, made with Claude & Gemini AI.

## Requirements

- Windows 10/11 or Linux
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Dependencies

The project uses:
- SQLite
- Avalonia 11.2
- Avalonia.Desktop 11.2
- Avalonia.Themes.Fluent 11.2
- Microsoft.Data.Sqlite 8


## Setup

Clone the repository:

```bash
git clone https://github.com/CallmeJef/POS.git
cd POS
```

Restore dependencies:

```bash
dotnet restore PosApp.Desktop/PosApp.Desktop.csproj
```

## Run

```bash
dotnet run --project PosApp.Desktop
```

## Build

```bash
dotnet build PosApp.Desktop/PosApp.Desktop.csproj
```

## Default Login

| Role | Username | Password |
|---|---|---|
| Admin | `UserAdmin` | `admin123` |
