
using System.IO;
using Kosync.Database.Entities;

namespace Kosync.Database;

public class KosyncDb
{
    public LiteDatabase Context { get; } = default!;

    public KosyncDb()
    {
        // Globální oprava pro LiteDB cast exception pod .NET 8
        // Zajišťuje, že pokud je v DB uloženo číslo jako Decimal, mapper ho dokáže načíst do Double a naopak.
        BsonMapper.Global.RegisterType<double>(
            serialize: v => new BsonValue(v),
            deserialize: v => v.IsNumber ? v.AsDouble : 0.0
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

        // Použití explicitního Query.EQ místo LINQ pro bezpečnější deserializaci při startu
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
