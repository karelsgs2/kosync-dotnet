
using System.IO;

namespace Kosync.Database;

public class KosyncDb
{
    public LiteDatabase Context { get; } = default!;

    public KosyncDb()
    {
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

        var userCollection = Context.GetCollection<User>("users");

        var adminUser = userCollection.FindOne(i => i.Username == "admin");
        if (adminUser is null)
        {
            adminUser = new User()
            {
                Username = "admin",
                IsAdministrator = true,
            };
            userCollection.Insert(adminUser);
        }

        adminUser.PasswordHash = Utility.HashPassword(adminPassword);

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
