# record2.py <seconds> <log> <recorder command...>: runs a recorder for that long, then stops it with SIGINT,
# the signal it finishes its file on. `xcrun simctl io <device> recordVideo` only writes a playable movie
# when interrupted, and a plain kill leaves a file that never finishes.
import os, signal, subprocess, sys, time
seconds = float(sys.argv[1])
log = open(sys.argv[2], 'w')
recorder = subprocess.Popen(sys.argv[3:], stdout=log, stderr=subprocess.STDOUT, start_new_session=True,
                            preexec_fn=lambda: signal.signal(signal.SIGINT, signal.SIG_DFL))
time.sleep(seconds)
tree = subprocess.run(['pgrep', '-g', str(os.getpgid(recorder.pid))], capture_output=True, text=True).stdout.split()
print('group', recorder.pid, tree)
os.killpg(os.getpgid(recorder.pid), signal.SIGINT)
print('exit', recorder.wait())
