namespace Models;

[GenerateSerializer]
public partial class UserProfile
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public DateTime CreatedAt { get; set; }
}