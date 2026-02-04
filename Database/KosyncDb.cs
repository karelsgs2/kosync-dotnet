
using System.IO;
using Kosync.Database.Entities;

namespace Kosync.Database;

public class KosyncDb
{
    public LiteDatabase Context { get; } = default!;

    public KosyncDb()
    {
        // Kritická oprava pro .NET 8: Explicitní mapování číselných typů.
        // Pokud LiteDB najde v DB Decimal, převede ho na Double pro naše entity.
        BsonMapper.Global.RegisterType<double>(
            serialize: v => new BsonValue(v),
            deserialize: v => {
                if (v.IsNumber) return v.AsDouble;
                if (v.IsDecimal) return (double)v.AsDecimal;
                return 0.0;
            }
        );

        // Zajištění, že složka pro databázi existuje
        if (!Directory.Exists("data"))
        {
            Directory.CreateDirectory("data");
        }

        Context = new LiteDatabase("Filename=data/Kosync.db;Connection=shared");
        CreateDefaults();
    }

    public void CreateDefaults()
    {
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        if (adminPassword is null)
        {
            adminPassword = "admin";
        }

        var userCollection = Context.GetCollection<DbUser>("users");

        // Hledání admina bez LINQ pro maximální stabilitu při inicializaci
        var adminUser = userCollection.FindOne(Query.EQ("Username", "admin"));
        
        if (adminUser is null)
        {
            adminUser = new DbUser()
            {
                Username = "admin",
                IsAdministrator = true,
                IsActive = true
            };
            userCollection.Insert(adminUser);
        }

        adminUser.PasswordHash = Utility.HashPassword(adminPassword);
        adminUser.IsAdministrator = true;

        userCollection.Update(adminUser);
        userCollection.EnsureIndex(i => i.Username);

        // Inicializace systémových nastavení
        var settingsCollection = Context.GetCollection<SystemSetting>("system_settings");
        settingsCollection.EnsureIndex(s => s.Key, true);

        var regDisabled = settingsCollection.FindOne(s => s.Key == "RegistrationDisabled");
        if (regDisabled is null)
        {
            var envVal = Environment.GetEnvironmentVariable("REGISTRATION_DISABLED");
            settingsCollection.Insert(new SystemSetting 
            { 
                Key = "RegistrationDisabled", 
                Value = (envVal != null && envVal.ToLower() == "true") ? "true" : "false" 
            });
        }
    }
}
