TRAIT = "smoke"

def run_test(tw_cli, context):
    # First we check that the "sytem" runner is defined
    result = tw_cli.run_command("supervisor", "send", "runners list", "-y", "--stdout-format=tooling")
    if result["returncode"] != 0 or not "system" in result["stdout"]:
        return "Runner 'system' is not running"
    
    # The we check that the required services have been registered
    result = tw_cli.run_command("contracts", "list", "--stdout-format=tooling")
    if result["returncode"] != 0:
        return "Cannot obtain the registered services"
    
    if not "Tinkwell.Orchestrator" in result["stdout"]:
        return f"Service 'Tinkwell.Orchestrator' is not running"
    
    if not "Tinkwell.Store" in result["stdout"]:
        return "Service 'Tinkwell.Store' is not running"

    # That's it, this is just smoke testing
    return True
