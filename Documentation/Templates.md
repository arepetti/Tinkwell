# Templates

Templates in Tinkwell CLI provide a powerful way to scaffold new projects, generate configuration files, or create any set of files based on user-defined inputs. They support both simple file copying and complex, conditional generation using meta-templates.

## Template Structure

A template is a directory containing a `template.json` manifest file and the files/folders that make up the template content. Templates are typically located in the `Templates` subdirectory of the `tw` executable's folder.

### `template.json` Manifest

This JSON file defines the template's metadata, questions, and the files to be processed. For meta-templates, it also defines a sequence of other templates to apply.

```json
{
  "id": "unique_template_id",
  "name": "Human-friendly Template Name",
  "description": "A brief description of what this template does.",
  "type": "standard" | "meta",
  "questions": [
    {
      "name": "VariableName",
      "prompt": "What should be the value of VariableName?",
      "type": "text" | "confirm" | "selection",
      "default": "DefaultValue",
      "options": ["Option1", "Option2"] // For type "selection"
    }
  ],
  "files": [
    { "original": "path/to/source.txt", "target": "path/to/{{ VariableName }}.txt", "mode": "copy" | "append" | "unspecified" }
  ], // Only for "standard" templates
  "sequence": [
    { "templateId": "another_template_id", "when": "Expression Here" }
  ] // Only for "meta" templates
}
```

You do not need to create the `template.json` file yourself, use `tw templates create` to build one interactively.

#### Manifest Properties:

*   **`id`** (string, required): A unique, C-style identifier for the template (e.g., `console_app`, `full_system_setup`). This is used for programmatic reference.
*   **`name`** (string, required): A human-friendly, descriptive name for the template.
*   **`description`** (string, required): A brief explanation of the template's purpose.
*   **`type`** (string, required): Specifies the template's behavior:
    *   `"standard"`: A regular template that copies and renders files.
    *   `"meta"`: A meta-template that orchestrates the application of other templates.
*   **`questions`** (array of objects, optional): Defines the variables the template needs and how to prompt the user for them.
    *   **`when`** (string, optional): An expression that determines if the question should be asked.
    *   **`name`** (string, required): The variable name (e.g., `ProjectName`, `Author`).
    *   **`prompt`** (string, required): The text displayed to the user when asking for input.
    *   **`type`** (string, required): The type of input prompt:
        *   `"text"`: A simple text input.
        *   `"confirm"`: A yes/no confirmation (e.g., `[Y/n]`).
        *   `"selection"`: A list of options for the user to choose from.
    *   **`default`** (string, optional): The default value for the variable if the user doesn't provide one. For `"confirm"` type, use `"yes"` or `"no"`.
    *   **`options`** (array of strings, optional): Required for `"selection"` type, providing the list of choices.
*   **`files`** (array of objects, optional): **Only for `"standard"` templates.** Defines which files from the template directory should be copied and how they should be named and handled in the target directory.
    *   **`when`** (string, optional): An expression that determines if the file should be processed.
    *   **`original`** (string, required): The path to the source file within the template's directory (relative to `template.json`).
    *   **`target`** (string, required): The desired path and filename in the output directory. This string is processed as a Liquid template, allowing for dynamic naming (e.g., `"src/{{ ProjectName }}.cs"`). The full path is created if it does not exist.
    *   **`mode`** (string, optional): How the file should be handled if it already exists in the target location:
        *   `"copy"`: Overwrites the existing file. This is the default if the template is applied directly (not as part of a meta-template).
        *   `"append"`: Appends the new content to the end of the existing file. If the file doesn't exist, it's created.
        *   `"unspecified"`: The mode is determined by the context: `"copy"` for direct template application, `"append"` if applied as part of a meta-template sequence.
*   **`sequence`** (array of objects, optional): **Only for `"meta"` templates.** Defines a list of other templates (by their `id`) to apply, potentially conditionally.
    *   **`when`** (string, optional): An NCalc expression that, if evaluates to `false`, will skip this step. The expression can reference variables from any previously applied template using dot notation (e.g., `"[full_system_setup.include_mqtt] == true"`).
    *   **`templateId`** (string, required): The `id` of the template to apply.

## Template Rendering (Liquid Templates)

File names (`target` in `files` array) and file contents are processed as [Liquid templates](https://shopify.github.io/liquid/). This allows you to embed variables and simple logic directly into your template files.

### Accessing Variables

Variables defined in the `questions` section of `template.json` (and answered by the user or provided via `--input`/`--set`) are available in the Liquid context. For example, if you have a question with `"name": "ProjectName"`, you can reference it in your template files like this:

```liquid
Hello, {{ ProjectName }}!
```

Note: when accessing a variable in an expression you have to use its fully qualified name (`[template_id.variable_name]` because
in expressions you have access to all the variables declared in a meta template. When performing text substitution in a text file you can access only variables defined in the current template and then you do not need the fully qualified name.

### Examples of Liquid Usage:

*   **Variable Substitution:**
    ```liquid
    // MyProject/{{ ProjectName }}.csproj
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>{{ Framework }}</TargetFramework>
      </PropertyGroup>
    </Project>
    ```

*   **Conditional Content:**
    ```liquid
    {% if include_logging %}
    // Add logging configuration
    {% endif %}
    ```

*   **Iteration:**
    ```liquid
    {% for item in items %}
    - {{ item.Name }}
    {% endfor %}
    ```

## Expressions for `when` Conditions

The `when` property in a meta-template's `sequence` uses [expressions](./Expressions.md) for conditional logic. These expressions are evaluated against the consolidated set of answers from all templates processed so far.

```text
"[full_system_setup.include_mqtt] == true && [runner.project_name] != \"MyOldProject\""
```

Note that `when` conditions (in questions, files and sections) must always use the fully qualified variable name.

## Command Line Usage

For detailed command-line usage, including all options like `--input`, `--set`, `--unattended`, `--dry-run`, and `--trace`, please refer to the [`tw templates` section in CLI.md](./CLI.md#tw-templates).
