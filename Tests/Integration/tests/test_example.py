# tests/test_example.py

TRAIT = "integration"

def run_test(tw_cli):
    """
    Example integration test.
    """
    print("    Test Case: Example test running...")
    # Replace with actual CLI commands and assertions
    result = tw_cli.run_command("runners list")
    if result["returncode"] == 0 and "system" in result["stdout"]:
        print("      Example test PASSED.")
        return True
    else:
        # Example of returning a failure message
        return f"CLI 'version' command failed. Return code: {result["returncode"]}, Stdout: {result["stdout"].strip()}, Stderr: {result["stderr"].strip()}"