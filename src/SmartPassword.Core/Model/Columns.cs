namespace SmartPassword.Core.Model;

/// <summary>Column names of the Danish 1Password CSV export and how they map to <see cref="PasswordEntry"/>.</summary>
public static class Columns
{
    public const string Name = "Navn";
    public const string Category = "Kategori";
    public const string Username = "Brugernavn";
    public const string Password = "Password";
    public const string Website = "Website";
    public const string Otp = "OTP/2FA";
    public const string Notes = "Noter";
    public const string OtherFields = "Andre felter";
    public const string Tags = "Tags";
    public const string Favorite = "Favorit";
    public const string Created = "Oprettet";
    public const string Updated = "Opdateret";
    public const string Id = "Id";

    public static readonly IReadOnlyList<string> Standard =
        [Name, Category, Username, Password, Website, Otp, Notes, OtherFields, Tags, Favorite, Created, Updated, Id];

    public static string Get(PasswordEntry e, string column) => column switch
    {
        Name => e.Name,
        Category => e.Category,
        Username => e.Username,
        Password => e.Password,
        Website => e.Website,
        Otp => e.Otp,
        Notes => e.Notes,
        OtherFields => e.OtherFields,
        Tags => e.Tags,
        Favorite => e.Favorite,
        Created => e.Created,
        Updated => e.Updated,
        Id => e.Id,
        _ => e.ExtraColumns.GetValueOrDefault(column, ""),
    };

    public static void Set(PasswordEntry e, string column, string value)
    {
        switch (column)
        {
            case Name: e.Name = value; break;
            case Category: e.Category = value; break;
            case Username: e.Username = value; break;
            case Password: e.Password = value; break;
            case Website: e.Website = value; break;
            case Otp: e.Otp = value; break;
            case Notes: e.Notes = value; break;
            case OtherFields: e.OtherFields = value; break;
            case Tags: e.Tags = value; break;
            case Favorite: e.Favorite = value; break;
            case Created: e.Created = value; break;
            case Updated: e.Updated = value; break;
            case Id: e.Id = value; break;
            default: e.ExtraColumns[column] = value; break;
        }
    }
}
