
namespace Kosync.Database;

public class KosyncDb
{
    public LiteDatabase Context { get; } = default!;

    public KosyncDb()
    {
        // Zajistíme existenci složky data
        if (!Directory.Exists("data")) Directory.CreateDirectory("data");
        
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

        string hashed = Utility.HashPassword(adminPassword);
        if (adminUser.PasswordHash != hashed)
        {
            adminUser.PasswordHash = hashed;
            userCollection.Update(adminUser);
        }
        
        userCollection.EnsureIndex(i => i.Username);

        // Inicializace systémových nastavení
        var settingsCollection = Context.GetCollection<SystemSetting>("system_settings");
        
        // Prevence pádu při vytváření unikátního indexu: pokud jsou v DB duplicity, EnsureIndex by selhal.
        // Odstraníme případné duplicity pro klíč RegistrationDisabled předem.
        var allRegSettings = settingsCollection.Find(s => s.Key == "RegistrationDisabled").ToList();
        if (allRegSettings.Count > 1)
        {
            // Ponecháme jen ten s nejvyšším ID
            var toKeep = allRegSettings.OrderByDescending(s => s.Id).First();
            foreach (var s in allRegSettings.Where(x => x.Id != toKeep.Id))
            {
                settingsCollection.Delete(s.Id);
            }
        }

        try {
            settingsCollection.EnsureIndex(s => s.Key, true);
        } catch {
            // Pokud index selže, aplikace aspoň nespadne a zkusíme to příště
        }

        var regDisabled = settingsCollection.FindOne(s => s.Key == "RegistrationDisabled");
        if (regDisabled is null)
        {
            var envVal = Environment.GetEnvironmentVariable("REGISTRATION_DISABLED");
            string defaultValue = (envVal != null && envVal.ToLower() == "true") ? "true" : "false";
            
            settingsCollection.Insert(new SystemSetting 
            { 
                Key = "RegistrationDisabled", 
                Value = defaultValue 
            });
        }
    }
}
