import time

TRAIT = "integration"
TEST_PRIORITY = 20

def run_test(tw_cli, context):
    result = tw_cli.run_command("contracts", "list", "--stdout-format=tooling")
    if result["returncode"] != 0:
        return "Cannot obtain the registered services"
    
    if not "Tinkwell.Store" in result["stdout"]:
        return f"Service 'Tinkwell.Store' is not running"
    
    result = tw_cli.run_command("measures", "write", "voltage", "10 V", "--stdout-format=tooling")
    if result["returncode"] != 0:
        return "Cannot update a constant measure"

    result = tw_cli.run_command("measures", "write", "current", "5 A", "--stdout-format=tooling")
    if result["returncode"] != 0:
        return "Cannot update a constant measure"

    # This shouldn't be necessary (the time neede to start/stop the child process
    # is more than enough) but it's better safe than sorry
    time.sleep(1)

    result = tw_cli.run_command("measures", "read", "power", "--stdout-format=tooling")
    if result["returncode"] != 0:
        return "Cannot read a derived measure"

    if int(result["stdout"]) != 50:
        return f"Measure 'power' should be 50 W but it's {result["stdout"]} W"

    return True
