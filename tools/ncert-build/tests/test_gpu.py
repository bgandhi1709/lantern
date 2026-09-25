from datetime import datetime, time

from ncert_build.gpu import seconds_until_open

NIGHT = (time(20, 0), time(8, 0))


def test_inside_a_window_past_midnight_there_is_no_wait():
    assert seconds_until_open(datetime(2026, 9, 25, 1, 5), NIGHT) == 0
    assert seconds_until_open(datetime(2026, 9, 25, 21, 0), NIGHT) == 0


def test_during_the_day_the_run_waits_for_eight_pm():
    assert seconds_until_open(datetime(2026, 9, 25, 8, 0), NIGHT) == 12 * 3600
    assert seconds_until_open(datetime(2026, 9, 25, 19, 30), NIGHT) == 30 * 60


def test_no_window_means_any_hour():
    assert seconds_until_open(datetime(2026, 9, 25, 12, 0), None) == 0
