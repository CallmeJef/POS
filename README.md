# POS (Point of Sale)

**Description**
A POS System, made with Claude & Gemini AI.

This desktop Point of Sale application is built with C#, Avalonia UI, and SQLite.

## Requirements

- Windows 10/11 or Linux
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or Visual Studio Code
- Internet connection during the first build to restore packages

## Dependencies

The project uses:
- Avalonia 11.2
- Avalonia.Desktop 11.2
- Avalonia.Themes.Fluent 11.2
- Microsoft.Data.Sqlite 8
- SQLite

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
