using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventHub;

public static class Seed
{
    public static async Task Run(AppDb db, IConfiguration config, IWebHostEnvironment env)
    {
        if (!env.IsDevelopment() && !config.GetValue("SeedDemo", false))
        {
            var email = config["Bootstrap:AdminEmail"];
            var password = config["Bootstrap:AdminPassword"];
            if (!string.IsNullOrEmpty(email) && !await db.Users.AnyAsync(x => x.Email == email.ToLowerInvariant()))
            {
                if (password == null || password.Length < 12)
                    throw new InvalidOperationException("Bootstrap admin password must be at least 12 characters.");
                var user = new User { Name = "Platform administrator", Email = email.ToLowerInvariant(), Role = "Admin" };
                user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }
            if (!config.GetValue("SeedCatalog", false)) return;
        }
        var demo = env.IsDevelopment() || config.GetValue("SeedDemo", false);
        if (await db.Events.AnyAsync()) return;
        if (demo && await db.Users.AnyAsync(x => x.Id == "demo-organizer"))
            return;
        var hasher = new PasswordHasher<User>();
        var users = new[] { new User { Id = "demo-customer", Name = "Maya Rahman", Email = "maya@example.com" }, new User { Id = "demo-organizer", Name = "EventHub Live", Email = "host@eventhub.test", Role = "Organizer" }, new User { Id = "demo-admin", Name = "Platform Admin", Email = "admin@eventhub.test", Role = "Admin" }, new User { Id = "demo-staff", Name = "Venue Team", Email = "gate@eventhub.test", Role = "Staff" } };
        foreach (var u in users)
            u.PasswordHash = hasher.HashPassword(u, "EventHub!2026");
        var organizerId = "demo-organizer";
        if (demo) db.Users.AddRange(users);
        else {
            organizerId = "catalog-curator";
            if (!await db.Users.AnyAsync(x => x.Id == organizerId)) {
                var curator = new User { Id = organizerId, Name = "EventHub Live", Email = "catalog@eventhub.invalid", Role = "Organizer" };
                curator.PasswordHash = hasher.HashPassword(curator, Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)));
                db.Users.Add(curator);
            }
        }
        var venues = new[] { new Venue { Name = "ICCB, Hall 4", City = "Dhaka", Address = "International Convention City Bashundhara, Dhaka 1229", Rows = 10, SeatsPerRow = 12 }, new Venue { Name = "Army Stadium", City = "Dhaka", Address = "Dhaka Cantonment, Dhaka 1206", Rows = 12, SeatsPerRow = 14 }, new Venue { Name = "Bangladesh Shilpakala Academy", City = "Dhaka", Address = "14/3 Segunbagicha, Dhaka 1000", Rows = 8, SeatsPerRow = 12 }, new Venue { Name = "Radisson Blu Chattogram", City = "Chattogram", Address = "SS Khaled Road, Chattogram", Rows = 8, SeatsPerRow = 10 } };
        db.Venues.AddRange(venues);
        await db.SaveChangesAsync();
        var day = DateTime.UtcNow.Date.AddDays(21);
        var events = new[]{
            new Event{Title="Dhaka After Dark",Slug="dhaka-after-dark",Category="Music",Artist="Arnob · Shironamhin · Meghdol",VenueId=1,StartsAt=day.AddHours(12),EndsAt=day.AddHours(17),Featured=true,MinPrice=150000,Image="/images/concert.jpg",Description="One city. One night. A thousand voices. Join Bangladesh’s most loved artists for an unforgettable evening of live music, immersive lights, and the songs that bring us together.\n\nExperience Arnob’s soulful melodies, Shironamhin’s legendary anthems, and Meghdol’s atmospheric sound on a spectacular stage. An evening made for singing your heart out."},
            new Event{Title="The Sound of Tomorrow",Slug="sound-of-tomorrow",Category="Music",Artist="The Midnight Collective",VenueId=2,StartsAt=day.AddDays(3).AddHours(12),EndsAt=day.AddDays(3).AddHours(17),MinPrice=200000,Image="/images/festival.jpg",Description="Discover the next wave of electronic music. A night of spectacular sound, open skies, and unforgettable performances from the region’s most exciting artists."},
            new Event{Title="A Little More Laughter",Slug="a-little-more-laughter",Category="Comedy",Artist="Naveed Mahbub & friends",VenueId=3,StartsAt=day.AddDays(5).AddHours(13),EndsAt=day.AddDays(5).AddHours(15),MinPrice=80000,Image="/images/comedy.jpg",Description="Leave the week behind with an evening of fresh observations, familiar stories, and wonderfully unexpected punchlines. Featuring a handpicked lineup of local comedy favorites."},
            new Event{Title="Build What’s Next",Slug="build-whats-next",Category="Technology",Artist="Bangladesh Tech Summit",VenueId=1,StartsAt=day.AddDays(7).AddHours(3),EndsAt=day.AddDays(7).AddHours(12),MinPrice=250000,Image="/images/tech.jpg",Description="A day for the curious. Meet the builders shaping the future, get hands-on with new technology, and make connections that outlast the conference."},
            new Event{Title="An Evening of Stories",Slug="evening-of-stories",Category="Arts & Theatre",Artist="Dhaka Theatre Company",VenueId=3,StartsAt=day.AddDays(9).AddHours(12),EndsAt=day.AddDays(9).AddHours(15),MinPrice=60000,Image="/images/theatre.jpg",Description="Stories that stay with you. An intimate performance exploring memory, belonging, and the beautiful complexities of everyday life."},
            new Event{Title="Coastal Food & Culture Fest",Slug="coastal-food-culture",Category="Food & Culture",Artist="The Coastal Collective",VenueId=4,StartsAt=day.AddDays(11).AddHours(5),EndsAt=day.AddDays(11).AddHours(16),MinPrice=120000,Image="/images/food.jpg",Description="Come hungry, leave inspired. A celebration of coastal flavors, local craftsmanship, and live music in the heart of Chattogram."},
            new Event{Title="The City Run",Slug="the-city-run",Category="Sports",Artist="Dhaka Runners",VenueId=2,StartsAt=day.AddDays(14).AddHours(1),EndsAt=day.AddDays(14).AddHours(5),MinPrice=100000,Image="/images/sports.jpg",Description="A morning that moves the city. Cheer on your favorite runners from your reserved grandstand seat, with live race commentary and a community celebration at the finish line."},
            new Event{Title="Jazz Under the Stars",Slug="jazz-under-the-stars",Category="Music",Artist="The Bengal Jazz Quartet",VenueId=4,StartsAt=day.AddDays(16).AddHours(13),EndsAt=day.AddDays(16).AddHours(17),MinPrice=180000,Image="/images/jazz.jpg",Description="An easy evening of extraordinary music. Let the quartet take you from timeless standards to new discoveries, with a little improvisation along the way."}
        };
        foreach (var e in events)
        {
            e.Status = "PUBLISHED";
            e.OrganizerId = organizerId;
            e.VenueId = venues[e.VenueId - 1].Id;
        }
        db.Events.AddRange(events);
        await db.SaveChangesAsync();
        foreach (var e in events)
            CreateSeats(db, e, venues.Single(x => x.Id == e.VenueId));
        if (demo) Store.Notify(db, "demo-customer", "Welcome to EventHub", "Your next great memory is waiting. Explore live events near you.");
        await db.SaveChangesAsync();
    }
    public static void CreateSeats(AppDb db, Event ev, Venue venue)
    {
        for (var row = 0; row < venue.Rows; row++)
        for (var n = 1; n <= venue.SeatsPerRow; n++)
        {
            var vipRows = venue.Rows == 1 ? 0 : Math.Max(1, venue.Rows / 5);
            var premiumRows = venue.Rows <= 2 ? 0 : Math.Max(1, venue.Rows * 3 / 10);
            var premium = row < vipRows;
            var middle = row < vipRows + premiumRows;
            db.Seats.Add(new()
            {
                EventId = ev.Id,
                Id = row * venue.SeatsPerRow + n,
                Row = ((char)('A' + row)).ToString(),
                Number = n,
                Section = premium ? "VIP" : middle ? "Premium" : "Standard",
                Price = premium ? ev.MinPrice * 2 : middle ? (ev.MinPrice * 3) / 2 : ev.MinPrice
            });
        }
    }
}
