namespace BingCook.Api.Tests.Data;

public sealed class PostgresUserRepositoryTests
{
    [Fact]
    public void CreateAsync_casts_role_parameter_to_postgres_user_role_enum()
    {
        var repositorySource = File.ReadAllText(FindRepositorySourcePath());

        Assert.Contains("@role::user_role", repositorySource);
    }

    private static string FindRepositorySourcePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "Data",
                "PostgresUserRepository.cs");

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find PostgresUserRepository.cs.");
    }
}
