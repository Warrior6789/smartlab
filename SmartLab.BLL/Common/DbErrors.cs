using Npgsql;

namespace SmartLab.BLL.Common;

/// <summary>Database errors that come from bad client input rather than a server fault.</summary>
public static class DbErrors
{
    /// <summary>SQLSTATE 22021: PostgreSQL text cannot hold some characters, e.g. NUL (\0) sent in a search or a name.</summary>
    private const string CharacterNotInRepertoire = "22021";

    /// <summary>True when the request carried text PostgreSQL rejects; the API answers 400 instead of 500.</summary>
    public static bool IsInvalidText(Exception ex)
    {
        if (ex is PostgresException { SqlState: CharacterNotInRepertoire })
            return true;

        return ex.InnerException is PostgresException { SqlState: CharacterNotInRepertoire };
    }
}
