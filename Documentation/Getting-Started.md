# Getting Started with Tinkwell

This guide provides a step-by-step walkthrough to get a basic Tinkwell instance up and running.

## 1. Generate the Development Certificate

Before running the Supervisor, you need a valid HTTPS certificate.

-   Follow the instructions in the [Setup guide](./Setup.md) to generate and install the necessary certificate.

## 2. Understand the Ensemble Configuration

The `ensamble.tw` file is the heart of your application's configuration, defining all the processes (runners) that the Supervisor will manage.

-   Review the [Ensemble Syntax documentation](./Ensamble.md) to understand how runners and firmlets are defined.
-   Open the default `ensamble.tw` file in the `Assets` directory to see a practical example.

## 3. Start the Supervisor

The Supervisor is the main entry point of your application.

-   Launch the `Tinkwell.Supervisor` project from your development environment (e.g., Visual Studio).
-   The Supervisor will read the `ensamble.tw` file and start all the configured runners.

## 4. Verify the System

Once the Supervisor is running, you can use the command-line interface (CLI) to inspect the system.

-   Open a terminal and run `tw runners list` to see all the active runners.
-   This command communicates with the Supervisor to get the status of the system.

## Next Steps

You now have a running Tinkwell system. To learn more, explore the following resources:

-   **[Glossary](./Glossary.md):** Understand the core concepts and terminology.
-   **[CLI Reference](./CLI.md):** Discover all the available commands for monitoring and debugging.
-   **[Derived Measures](./Derived-measures.md):** Learn how to create custom measures.
