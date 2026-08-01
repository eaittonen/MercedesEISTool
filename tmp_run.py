import subprocess, sys, os
cmd = sys.argv[1:]
log_path = os.path.join(os.getcwd(), 'tmp_run.log')
with open(log_path, 'w', encoding='utf-8') as fh:
    proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    fh.write(proc.stdout)
    fh.write(f'\nEXIT_CODE={proc.returncode}\n')
print(log_path)
