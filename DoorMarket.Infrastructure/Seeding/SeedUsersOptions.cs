namespace DoorMarket.Infrastructure.Seeding;

public class SeedUsersOptions
{
    public SeedUser Admin { get; set; } = new();
    public SeedUser Shop { get; set; } = new();
    public SeedUser Client { get; set; } = new();
}

public class SeedUser
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Phone { get; set; }
}
