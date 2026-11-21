using System.Globalization;
using Microsoft.Data.Sqlite;
using PosApp.Models;

namespace PosApp.Services;

/// <summary>
/// All SQLite access. The database file lives in the app's local data folder, so it is created
/// automatically on first run and survives restarts (Windows, Linux and Android).
/// Money is stored as integer cents to avoid rounding errors.
/// </summary>
public static class Db
{
    public const int MinSecretLength = 6;
    public const string DefaultAdminUsername = "UserAdmin";
    public const string DefaultAdminPassword = "admin123";
    public const string DefaultVoidCode = "123456";

    static string _connectionString = "";
    public static string DatabasePath { get; private set; } = "";

    const string Schema = @"
CREATE TABLE IF NOT EXISTS Users(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE COLLATE NOCASE,
    PasswordHash TEXT NOT NULL,
    Role TEXT NOT NULL CHECK(Role IN ('Admin','Cashier')),
    VoidCodeHash TEXT NULL
);
CREATE TABLE IF NOT EXISTS Categories(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE COLLATE NOCASE
);
CREATE TABLE IF NOT EXISTS Items(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CategoryId INTEGER NOT NULL REFERENCES Categories(Id) ON DELETE CASCADE,
    Name TEXT NOT NULL,
    PriceCents INTEGER NOT NULL,
    Image BLOB NULL
);
CREATE TABLE IF NOT EXISTS CustomOptions(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ItemId INTEGER NOT NULL REFERENCES Items(Id) ON DELETE CASCADE,
    Kind TEXT NOT NULL CHECK(Kind IN ('Size','Addition')),
    Name TEXT NOT NULL,
    PriceCents INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS PaymentMethods(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Details TEXT NOT NULL DEFAULT '',
    Image BLOB NULL
);
CREATE TABLE IF NOT EXISTS Orders(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CreatedAt TEXT NOT NULL,
    CashierName TEXT NOT NULL,
    DiscountType TEXT NULL,
    DiscountCents INTEGER NOT NULL DEFAULT 0,
    TotalCents INTEGER NOT NULL,
    AmountPaidCents INTEGER NOT NULL DEFAULT 0,
    PaymentMethod TEXT NULL,
    QueueNumber INTEGER NOT NULL DEFAULT 0,
    Status TEXT NOT NULL DEFAULT 'Pending',
    VoidedAt TEXT NULL,
    VoidedBy TEXT NULL
);
CREATE TABLE IF NOT EXISTS OrderLines(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    OrderId INTEGER NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE,
    ItemName TEXT NOT NULL,
    Options TEXT NOT NULL DEFAULT '',
    Qty INTEGER NOT NULL,
    LineTotalCents INTEGER NOT NULL
);
";

    // ------------------------------------------------------------------ setup

    public static void Init()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PosApp");
        Directory.CreateDirectory(dir);
        DatabasePath = Path.Combine(dir, "pos.db");
        _connectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString();

        using var c = Open();
        using (var cmd = Cmd(c, null, Schema)) cmd.ExecuteNonQuery();
        MigrateOrders(c);
        SeedAdmin(c);
    }

    /// <summary>Upgrades a database created before queue numbers / order status existed.</summary>
    static void MigrateOrders(SqliteConnection c)
    {
        var have = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var info = Cmd(c, null, "PRAGMA table_info(Orders)"))
        using (var r = info.ExecuteReader())
            while (r.Read()) have.Add(r.GetString(1));

        void AddColumn(string name, string definition)
        {
            if (have.Contains(name)) return;
            using var alter = Cmd(c, null, $"ALTER TABLE Orders ADD COLUMN {name} {definition}");
            alter.ExecuteNonQuery();
        }

        AddColumn("AmountPaidCents", "INTEGER NOT NULL DEFAULT 0");
        AddColumn("QueueNumber", "INTEGER NOT NULL DEFAULT 0");
        // Orders that existed before the queue was introduced are treated as already served.
        AddColumn("Status", "TEXT NOT NULL DEFAULT 'Served'");
        AddColumn("VoidedAt", "TEXT NULL");
        AddColumn("VoidedBy", "TEXT NULL");
    }

    static void SeedAdmin(SqliteConnection c)
    {
        using (var check = Cmd(c, null, "SELECT COUNT(*) FROM Users WHERE Role='Admin'"))
        {
            if (Convert.ToInt64(check.ExecuteScalar()) > 0) return;
        }
        using var ins = Cmd(c, null,
            "INSERT INTO Users(Username, PasswordHash, Role, VoidCodeHash) VALUES(@u, @p, 'Admin', @v)",
            ("@u", DefaultAdminUsername),
            ("@p", PasswordHasher.Hash(DefaultAdminPassword)),
            ("@v", PasswordHasher.Hash(DefaultVoidCode)));
        ins.ExecuteNonQuery();
    }

    static SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        using var pragma = c.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return c;
    }

    static SqliteCommand Cmd(SqliteConnection c, SqliteTransaction? tx, string sql,
        params (string Name, object? Value)[] ps)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        if (tx != null) cmd.Transaction = tx;
        foreach (var (name, value) in ps)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    // ------------------------------------------------------------------ users

    public static UserAccount? Authenticate(string username, string password)
    {
        using var c = Open();
        using var cmd = Cmd(c, null,
            "SELECT Id, Username, Role, PasswordHash FROM Users WHERE Username = @u",
            ("@u", username.Trim()));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        if (!PasswordHasher.Verify(password, r.GetString(3))) return null;
        return new UserAccount { Id = r.GetInt64(0), Username = r.GetString(1), Role = r.GetString(2) };
    }

    public static List<UserAccount> GetCashiers()
    {
        var list = new List<UserAccount>();
        using var c = Open();
        using var cmd = Cmd(c, null,
            "SELECT Id, Username, Role FROM Users WHERE Role='Cashier' ORDER BY Username");
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new UserAccount { Id = r.GetInt64(0), Username = r.GetString(1), Role = r.GetString(2) });
        return list;
    }

    static bool UsernameTaken(SqliteConnection c, string username, long exceptId)
    {
        using var cmd = Cmd(c, null,
            "SELECT COUNT(*) FROM Users WHERE Username = @u AND Id <> @id",
            ("@u", username), ("@id", exceptId));
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    static string? ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return "Username is required.";
        if (username.Trim().Length > 40) return "Username must be 40 characters or fewer.";
        return null;
    }

    static string? ValidateSecret(string secret, string what) =>
        secret.Length < MinSecretLength ? $"{what} must be at least {MinSecretLength} characters." : null;

    /// <returns>An error message, or null on success.</returns>
    public static string? CreateCashier(string username, string password)
    {
        username = username.Trim();
        var err = ValidateUsername(username) ?? ValidateSecret(password, "Password");
        if (err != null) return err;

        using var c = Open();
        if (UsernameTaken(c, username, 0)) return "That username is already in use.";
        using var ins = Cmd(c, null,
            "INSERT INTO Users(Username, PasswordHash, Role) VALUES(@u, @p, 'Cashier')",
            ("@u", username), ("@p", PasswordHasher.Hash(password)));
        ins.ExecuteNonQuery();
        return null;
    }

    /// <param name="newPassword">Null/empty keeps the current password.</param>
    public static string? UpdateCashier(long id, string username, string? newPassword)
    {
        username = username.Trim();
        var err = ValidateUsername(username);
        if (err == null && !string.IsNullOrEmpty(newPassword)) err = ValidateSecret(newPassword, "Password");
        if (err != null) return err;

        using var c = Open();
        if (UsernameTaken(c, username, id)) return "That username is already in use.";
        if (string.IsNullOrEmpty(newPassword))
        {
            using var u = Cmd(c, null, "UPDATE Users SET Username=@u WHERE Id=@id AND Role='Cashier'",
                ("@u", username), ("@id", id));
            u.ExecuteNonQuery();
        }
        else
        {
            using var u = Cmd(c, null,
                "UPDATE Users SET Username=@u, PasswordHash=@p WHERE Id=@id AND Role='Cashier'",
                ("@u", username), ("@p", PasswordHasher.Hash(newPassword)), ("@id", id));
            u.ExecuteNonQuery();
        }
        return null;
    }

    public static void DeleteCashier(long id)
    {
        using var c = Open();
        using var cmd = Cmd(c, null, "DELETE FROM Users WHERE Id=@id AND Role='Cashier'", ("@id", id));
        cmd.ExecuteNonQuery();
    }

    /// <summary>Changes the admin username, and optionally the password and void code.</summary>
    public static string? UpdateAdmin(long id, string username, string currentPassword,
        string? newPassword, string? newVoidCode)
    {
        username = username.Trim();
        var err = ValidateUsername(username);
        if (err == null && !string.IsNullOrEmpty(newPassword)) err = ValidateSecret(newPassword, "New password");
        if (err == null && !string.IsNullOrEmpty(newVoidCode)) err = ValidateSecret(newVoidCode, "Void code");
        if (err != null) return err;

        using var c = Open();
        string? storedHash;
        using (var get = Cmd(c, null, "SELECT PasswordHash FROM Users WHERE Id=@id AND Role='Admin'", ("@id", id)))
            storedHash = get.ExecuteScalar() as string;
        if (!PasswordHasher.Verify(currentPassword, storedHash)) return "Current password is incorrect.";
        if (UsernameTaken(c, username, id)) return "That username is already in use.";

        using var tx = c.BeginTransaction();
        using (var u = Cmd(c, tx, "UPDATE Users SET Username=@u WHERE Id=@id", ("@u", username), ("@id", id)))
            u.ExecuteNonQuery();
        if (!string.IsNullOrEmpty(newPassword))
        {
            using var u = Cmd(c, tx, "UPDATE Users SET PasswordHash=@p WHERE Id=@id",
                ("@p", PasswordHasher.Hash(newPassword)), ("@id", id));
            u.ExecuteNonQuery();
        }
        if (!string.IsNullOrEmpty(newVoidCode))
        {
            using var u = Cmd(c, tx, "UPDATE Users SET VoidCodeHash=@v WHERE Id=@id",
                ("@v", PasswordHasher.Hash(newVoidCode)), ("@id", id));
            u.ExecuteNonQuery();
        }
        tx.Commit();
        return null;
    }

    /// <summary>Checks a typed void code against the admin's stored one (used by the cashier side later).</summary>
    public static bool VerifyVoidCode(string code)
    {
        using var c = Open();
        using var cmd = Cmd(c, null, "SELECT VoidCodeHash FROM Users WHERE Role='Admin' LIMIT 1");
        return PasswordHasher.Verify(code, cmd.ExecuteScalar() as string);
    }

    // ------------------------------------------------------------- categories

    public static List<Category> GetCategories()
    {
        var list = new List<Category>();
        using var c = Open();
        using var cmd = Cmd(c, null, "SELECT Id, Name FROM Categories ORDER BY Id");
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(new Category { Id = r.GetInt64(0), Name = r.GetString(1) });
        return list;
    }

    static string? ValidateCategoryName(SqliteConnection c, string name, long exceptId)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Category name is required.";
        if (name.Length > 40) return "Category name must be 40 characters or fewer.";
        using var cmd = Cmd(c, null, "SELECT COUNT(*) FROM Categories WHERE Name=@n AND Id <> @id",
            ("@n", name), ("@id", exceptId));
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0 ? "A category with that name already exists." : null;
    }

    public static string? AddCategory(string name, out long id)
    {
        id = 0;
        name = name.Trim();
        using var c = Open();
        var err = ValidateCategoryName(c, name, 0);
        if (err != null) return err;
        using var ins = Cmd(c, null, "INSERT INTO Categories(Name) VALUES(@n); SELECT last_insert_rowid();",
            ("@n", name));
        id = Convert.ToInt64(ins.ExecuteScalar());
        return null;
    }

    public static string? RenameCategory(long id, string name)
    {
        name = name.Trim();
        using var c = Open();
        var err = ValidateCategoryName(c, name, id);
        if (err != null) return err;
        using var cmd = Cmd(c, null, "UPDATE Categories SET Name=@n WHERE Id=@id", ("@n", name), ("@id", id));
        cmd.ExecuteNonQuery();
        return null;
    }

    /// <summary>Deletes the category together with its items and their customizations.</summary>
    public static void DeleteCategory(long id)
    {
        using var c = Open();
        using var cmd = Cmd(c, null, "DELETE FROM Categories WHERE Id=@id", ("@id", id));
        cmd.ExecuteNonQuery();
    }

    // ------------------------------------------------------------------ items

    public static List<Product> GetProducts(long categoryId)
    {
        var list = new List<Product>();
        using var c = Open();
        using var cmd = Cmd(c, null,
            "SELECT Id, CategoryId, Name, PriceCents, Image FROM Items WHERE CategoryId=@c ORDER BY Id",
            ("@c", categoryId));
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadProduct(r));
        return list;
    }

    static Product ReadProduct(SqliteDataReader r) => new()
    {
        Id = r.GetInt64(0),
        CategoryId = r.GetInt64(1),
        Name = r.GetString(2),
        Price = Money.FromCents(r.GetInt64(3)),
        Image = r.IsDBNull(4) ? null : r.GetFieldValue<byte[]>(4)
    };

    /// <summary>Loads one product including its Size and Addition options.</summary>
    public static Product? GetProduct(long id)
    {
        using var c = Open();
        Product? p;
        using (var cmd = Cmd(c, null,
            "SELECT Id, CategoryId, Name, PriceCents, Image FROM Items WHERE Id=@id", ("@id", id)))
        using (var r = cmd.ExecuteReader())
        {
            if (!r.Read()) return null;
            p = ReadProduct(r);
        }
        p.Options = GetOptions(c, id);
        return p;
    }

    public static List<CustomOption> GetOptionsForItem(long itemId)
    {
        using var c = Open();
        return GetOptions(c, itemId);
    }

    static List<CustomOption> GetOptions(SqliteConnection c, long itemId)
    {
        var list = new List<CustomOption>();
        using var cmd = Cmd(c, null,
            "SELECT Id, Kind, Name, PriceCents FROM CustomOptions WHERE ItemId=@i ORDER BY Kind, Id",
            ("@i", itemId));
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new CustomOption
            {
                Id = r.GetInt64(0),
                Kind = r.GetString(1),
                Name = r.GetString(2),
                PriceDelta = Money.FromCents(r.GetInt64(3))
            });
        return list;
    }

    /// <summary>Inserts (Id = 0) or updates a product and replaces its customization options.</summary>
    public static long SaveProduct(Product p)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        long id = p.Id;
        if (id == 0)
        {
            using var ins = Cmd(c, tx,
                "INSERT INTO Items(CategoryId, Name, PriceCents, Image) VALUES(@c, @n, @p, @img); SELECT last_insert_rowid();",
                ("@c", p.CategoryId), ("@n", p.Name), ("@p", Money.ToCents(p.Price)), ("@img", p.Image));
            id = Convert.ToInt64(ins.ExecuteScalar());
        }
        else
        {
            using var upd = Cmd(c, tx,
                "UPDATE Items SET Name=@n, PriceCents=@p, Image=@img WHERE Id=@id",
                ("@n", p.Name), ("@p", Money.ToCents(p.Price)), ("@img", p.Image), ("@id", id));
            upd.ExecuteNonQuery();
            using var del = Cmd(c, tx, "DELETE FROM CustomOptions WHERE ItemId=@id", ("@id", id));
            del.ExecuteNonQuery();
        }

        foreach (var o in p.Options)
        {
            using var io = Cmd(c, tx,
                "INSERT INTO CustomOptions(ItemId, Kind, Name, PriceCents) VALUES(@i, @k, @n, @p)",
                ("@i", id), ("@k", o.Kind), ("@n", o.Name), ("@p", Money.ToCents(o.PriceDelta)));
            io.ExecuteNonQuery();
        }
        tx.Commit();
        return id;
    }

    public static void DeleteProducts(IEnumerable<long> ids)
    {
        using var c = Open();
        using var tx = c.BeginTransaction();
        foreach (var id in ids)
        {
            using var cmd = Cmd(c, tx, "DELETE FROM Items WHERE Id=@id", ("@id", id));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    // --------------------------------------------------------- payment methods

    public static List<PaymentMethod> GetPaymentMethods()
    {
        var list = new List<PaymentMethod>();
        using var c = Open();
        using var cmd = Cmd(c, null, "SELECT Id, Name, Details, Image FROM PaymentMethods ORDER BY Id");
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new PaymentMethod
            {
                Id = r.GetInt64(0),
                Name = r.GetString(1),
                Details = r.GetString(2),
                Image = r.IsDBNull(3) ? null : r.GetFieldValue<byte[]>(3)
            });
        return list;
    }

    public static void AddPaymentMethod(string name, string details, byte[]? image)
    {
        using var c = Open();
        using var cmd = Cmd(c, null,
            "INSERT INTO PaymentMethods(Name, Details, Image) VALUES(@n, @d, @i)",
            ("@n", name.Trim()), ("@d", details.Trim()), ("@i", image));
        cmd.ExecuteNonQuery();
    }

    public static void DeletePaymentMethod(long id)
    {
        using var c = Open();
        using var cmd = Cmd(c, null, "DELETE FROM PaymentMethods WHERE Id=@id", ("@id", id));
        cmd.ExecuteNonQuery();
    }

    // ----------------------------------------------------------------- ledger

    /// <summary>All completed orders, newest first, with a readable item summary and total quantity.</summary>
    public static List<OrderRecord> GetOrders()
    {
        var orders = new List<OrderRecord>();
        var byId = new Dictionary<long, OrderRecord>();
        var lines = new Dictionary<long, List<string>>();

        using var c = Open();
        using (var cmd = Cmd(c, null,
            "SELECT Id, CreatedAt, CashierName, TotalCents FROM Orders WHERE Status <> 'Voided' ORDER BY CreatedAt DESC, Id DESC"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var o = new OrderRecord
                {
                    Id = r.GetInt64(0),
                    CreatedAt = DateTime.Parse(r.GetString(1), CultureInfo.InvariantCulture),
                    CashierName = r.GetString(2),
                    Total = Money.FromCents(r.GetInt64(3))
                };
                orders.Add(o);
                byId[o.Id] = o;
                lines[o.Id] = new List<string>();
            }
        }

        using (var cmd = Cmd(c, null, "SELECT OrderId, ItemName, Options, Qty FROM OrderLines ORDER BY Id"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var orderId = r.GetInt64(0);
                if (!byId.TryGetValue(orderId, out var o)) continue;
                var qty = r.GetInt32(3);
                var opts = r.GetString(2);
                o.Quantity += qty;
                lines[orderId].Add($"{qty}× {r.GetString(1)}" + (string.IsNullOrWhiteSpace(opts) ? "" : $" ({opts})"));
            }
        }

        foreach (var o in orders) o.Summary = string.Join("\n", lines[o.Id]);
        return orders;
    }

    // ------------------------------------------------------- orders and queue

    /// <summary>
    /// Saves a paid order and returns its queue number. Queue numbers start at 1 each day
    /// (voided orders keep their number so it is never handed out twice on the same day).
    /// </summary>
    public static int SaveOrder(NewOrder order)
    {
        var now = DateTime.Now;
        var dayPrefix = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "%";

        using var c = Open();
        using var tx = c.BeginTransaction();

        int queueNumber;
        using (var next = Cmd(c, tx,
            "SELECT COALESCE(MAX(QueueNumber), 0) + 1 FROM Orders WHERE CreatedAt LIKE @d",
            ("@d", dayPrefix)))
            queueNumber = Convert.ToInt32(next.ExecuteScalar());

        long orderId;
        using (var ins = Cmd(c, tx,
            @"INSERT INTO Orders(CreatedAt, CashierName, DiscountType, DiscountCents, TotalCents,
                                 AmountPaidCents, PaymentMethod, QueueNumber, Status)
              VALUES(@t, @cn, @dt, @dc, @tc, @ap, @pm, @q, 'Pending'); SELECT last_insert_rowid();",
            ("@t", now.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)),
            ("@cn", order.CashierName),
            ("@dt", string.IsNullOrEmpty(order.DiscountType) ? null : order.DiscountType),
            ("@dc", Money.ToCents(order.Discount)),
            ("@tc", Money.ToCents(order.Total)),
            ("@ap", Money.ToCents(order.AmountPaid)),
            ("@pm", order.PaymentMethod),
            ("@q", queueNumber)))
            orderId = Convert.ToInt64(ins.ExecuteScalar());

        foreach (var l in order.Lines)
        {
            using var il = Cmd(c, tx,
                "INSERT INTO OrderLines(OrderId, ItemName, Options, Qty, LineTotalCents) VALUES(@o, @n, @op, @q, @t)",
                ("@o", orderId), ("@n", l.ItemName), ("@op", l.Options),
                ("@q", l.Qty), ("@t", Money.ToCents(l.LineTotal)));
            il.ExecuteNonQuery();
        }

        tx.Commit();
        return queueNumber;
    }

    /// <summary>Orders that have been paid but not yet handed to the customer, oldest first.</summary>
    public static List<QueueOrder> GetQueue()
    {
        var list = new List<QueueOrder>();
        var byId = new Dictionary<long, (QueueOrder Order, List<string> Lines)>();

        using var c = Open();
        using (var cmd = Cmd(c, null,
            "SELECT Id, QueueNumber, CreatedAt, TotalCents FROM Orders WHERE Status='Pending' ORDER BY CreatedAt, Id"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                var q = new QueueOrder
                {
                    Id = r.GetInt64(0),
                    QueueNumber = r.GetInt32(1),
                    CreatedAt = DateTime.Parse(r.GetString(2), CultureInfo.InvariantCulture),
                    Total = Money.FromCents(r.GetInt64(3))
                };
                list.Add(q);
                byId[q.Id] = (q, new List<string>());
            }
        }

        if (list.Count == 0) return list;

        using (var cmd = Cmd(c, null,
            @"SELECT l.OrderId, l.ItemName, l.Options, l.Qty FROM OrderLines l
              JOIN Orders o ON o.Id = l.OrderId WHERE o.Status='Pending' ORDER BY l.Id"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                if (!byId.TryGetValue(r.GetInt64(0), out var entry)) continue;
                var opts = r.GetString(2);
                entry.Lines.Add($"{r.GetInt32(3)}× {r.GetString(1)}" + (string.IsNullOrWhiteSpace(opts) ? "" : $" ({opts})"));
            }
        }

        foreach (var (order, lines) in byId.Values) order.Summary = string.Join("\n", lines);
        return list;
    }

    /// <summary>The customer received the order: it leaves the queue but stays in the ledger.</summary>
    public static void MarkServed(long orderId)
    {
        using var c = Open();
        using var cmd = Cmd(c, null,
            "UPDATE Orders SET Status='Served' WHERE Id=@id AND Status='Pending'", ("@id", orderId));
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Voids a pending order after checking the admin's void code. A voided order leaves the queue
    /// and no longer counts in the ledger totals.
    /// </summary>
    /// <returns>An error message, or null on success.</returns>
    public static string? VoidOrder(long orderId, string voidCode, string voidedBy)
    {
        if (!VerifyVoidCode(voidCode)) return "Incorrect void code.";

        using var c = Open();
        using var cmd = Cmd(c, null,
            "UPDATE Orders SET Status='Voided', VoidedAt=@t, VoidedBy=@b WHERE Id=@id AND Status='Pending'",
            ("@t", DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)),
            ("@b", voidedBy), ("@id", orderId));
        return cmd.ExecuteNonQuery() == 0 ? "That order is no longer in the queue." : null;
    }
}
