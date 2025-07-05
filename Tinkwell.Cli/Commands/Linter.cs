using System.Reflection;
using System.Text;

namespace Tinkwell.Cli.Commands;

abstract class Linter
{
    public enum IssueSeverity
    {
        Minor,
        Warning,
        Error,
        Critical,
    }

    public record Issue(string Id, IssueSeverity Severity, string TargetType, string TargetName, string Message);

    public abstract class Rule
    {
        public virtual string Id
            => _id ??= ComputeCrc16(GetType().Name).ToString("00000");

        protected static Issue? Ok() => null;

        protected Issue Minor<TTarget>(string targetName, string message)
            => new Issue(Id, IssueSeverity.Minor, typeof(TTarget).Name, targetName, message);

        protected Issue Warning<TTarget>(string targetName, string message)
            => new Issue(Id, IssueSeverity.Warning, typeof(TTarget).Name, targetName, message);

        protected Issue Error<TTarget>(string targetName, string message)
            => new Issue(Id, IssueSeverity.Error, typeof(TTarget).Name, targetName, message);

        protected Issue Critical<TTarget>(string targetName, string message)
            => new Issue(Id, IssueSeverity.Critical, typeof(TTarget).Name, targetName, message);

        private string? _id;

        private static ushort ComputeCrc16(string input)
        {
            const ushort poly = 0xA001;
            ushort crc = 0xFFFF;

            foreach (byte b in Encoding.UTF8.GetBytes(input))
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    bool lsb = (crc & 1) == 1;
                    crc >>= 1;
                    if (lsb)
                        crc ^= poly;
                }
            }
            return crc;
        }
    }

    public string[] Exclusions { get; set; } = [];

    protected IEnumerable<T> FindRules<T>()
    {
        return Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(type => typeof(T).IsAssignableFrom(type) && typeof(Rule).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
            .Select(type => (T)Activator.CreateInstance(type)!)
            .Where(rule => !Exclusions.Contains(((Rule)(object)rule!).Id, StringComparer.Ordinal));
    }
}

abstract class Linter<TData> : Linter
{    
    public async Task<IEnumerable<Issue>> CheckAsync(string path)
    {
        try
        {
            TData data = await LoadFileAsync(path);
            List<Issue> issues = new();
            Lint(issues, data);
            return issues

                .OrderByDescending(x => x.Severity)
                .ThenBy(x => x.Id);
        }
        catch (Exception e)
        {
            return [new Issue("00000", IssueSeverity.Critical, "File", path, e.Message)];
        }
    }

    protected abstract Task<TData> LoadFileAsync(string path);

    protected abstract void Lint(IList<Issue> issues, TData data);
}
