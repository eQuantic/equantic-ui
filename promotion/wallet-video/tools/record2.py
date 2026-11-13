# record2.py <seconds> <log> <recorder command...>: runs a recorder for that long, then stops it with SIGINT,
# the signal it finishes its file on. `xcrun simctl io <device> recordVideo` only writes a playable movie
# when interrupted, and a plain kill leaves a file that never finishes.
import os, signal, subprocess, sys, time
seconds = float(sys.argv[1])
# The log lands beside the recording, under raw/, which git ignores and a fresh clone does not have.
os.makedirs(os.path.dirname(sys.argv[2]) or '.', exist_ok=True)
log = open(sys.argv[2], 'w')
recorder = subprocess.Popen(sys.argv[3:], stdout=log, stderr=subprocess.STDOUT, start_new_session=True,
                            preexec_fn=lambda: signal.signal(signal.SIGINT, signal.SIG_DFL))
time.sleep(seconds)
try:
    group = os.getpgid(recorder.pid)
    print('group', recorder.pid, subprocess.run(['pgrep', '-g', str(group)], capture_output=True, text=True).stdout.split())
    os.killpg(group, signal.SIGINT)
except ProcessLookupError:
    # The recorder ended before its time: its log says why, and its exit code is below.
    print('the recorder had already ended')
print('exit', recorder.wait())
