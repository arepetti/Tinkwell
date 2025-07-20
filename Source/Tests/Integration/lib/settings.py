# Password for the self-signed certificate. It does not
# really matter because it's generated for each test and it's used
# only to run that test.
DEFAULT_CERT_PASSWORD = "1234"

# Default base port for gRPC services. We use an OS provided
# port but we defult to this in case of errors.
DEFAULT_HOST_PORT = 5000

# Time to wait to start and stop the application.
INITIAL_WAIT_SECONDS = 5
GRACEFUL_SHUTDOWN_SECONDS = 5

# The application might take longer to start, we ping
# the supervisor to know if it's ready (at most MAX_PING_RETRIES,
# waiting RETRY_WAIT_SECONDS between each attempt).
RETRY_WAIT_SECONDS = 2
MAX_PING_RETRIES = 5
