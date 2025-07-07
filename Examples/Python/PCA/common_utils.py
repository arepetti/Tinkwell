import threading
import time

def run_until_key_press(main_function):
    stop_event = threading.Event()

    def wait_for_input():
        try:
            input()
        except KeyboardInterrupt:
            pass # Handle Ctrl+C gracefully
        finally:
            stop_event.set()

    input_thread = threading.Thread(target=wait_for_input)
    input_thread.daemon = True
    input_thread.start()

    try:
        main_function(stop_event)
    except KeyboardInterrupt:
        pass # Handle Ctrl+C gracefully
    except Exception as e:
        print(f"An unexpected error occurred in main function: {e}")
    finally:
        stop_event.set()
        time.sleep(0.1)
