using System.Text;
using System.Text.Json;

namespace Tinkwell.Cli.Commands.Templates.Manifest;

sealed class TraceFile
{
    public Dictionary<string, object> Answers { get; set; } = new();

    public async Task ImportAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ObjectJsonConverter());
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json, options);
        if (data is not null)
        {
            foreach (var entry in data)
                Answers.Add(entry.Key, entry.Value);
        }
    }

    public async Task SaveAsync(string path)
    {
        await File.WriteAllTextAsync(path,
            JsonSerializer.Serialize(Answers, new JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8);
    }

    public void Add(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            var parts = value.Split('=', 2);
            var keys = parts[0].Split('.');
            var currentDict = Answers;

            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (!currentDict.ContainsKey(keys[i]) || !(currentDict[keys[i]] is Dictionary<string, object>))
                    currentDict[keys[i]] = new Dictionary<string, object>();
                currentDict = (Dictionary<string, object>)currentDict[keys[i]];
            }
            currentDict[keys[^1]] = parts[1];
        }
    }

    public Dictionary<string, object> GetAnswersFor(string templateId)
    {
        var answers = new Dictionary<string, object>();
        if (Answers.ContainsKey(templateId) && Answers[templateId] is Dictionary<string, object> existingAnswers)
            answers = existingAnswers;
        else
            Answers[templateId] = answers;
        return answers;
    }

    public Dictionary<string, object> Flatten()
    {
        var flattenedAnswers = new Dictionary<string, object>();
        foreach (var templateEntry in Answers)
        {
            if (templateEntry.Value is Dictionary<string, object> templateSpecificAnswers)
            {
                foreach (var answerEntry in templateSpecificAnswers)
                    flattenedAnswers[$"{templateEntry.Key}.{answerEntry.Key}"] = answerEntry.Value;
            }
        }
        return flattenedAnswers;
    }
}