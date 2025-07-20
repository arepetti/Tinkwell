def calculate_elapsed_time(start, end):
    elapsed_time = end - start
    minutes = int(elapsed_time // 60)
    seconds = int(elapsed_time % 60)

    time_str = ""
    if minutes > 0:
        time_str = f"{minutes} minute{'s' if minutes > 1 else ''} "
    return time_str + f"{seconds} second{'s' if seconds > 1 else ''}"