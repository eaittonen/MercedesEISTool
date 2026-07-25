import sqlite3
import os

paths = [
    r'C:\var\lib\mercedes-eis-tool\database\mercedes-eis-auth.db',
    r'C:\Users\Eetu Aittonen\source\repos\MercedesEISTool\MercedesEISTool.Server\Data\mercedes-eis-auth.db',
    r'C:\Users\Eetu Aittonen\source\repos\MercedesEISTool\MercedesEISTool.Server\mercedes-eis-auth.db',
]

for path in paths:
    print('CHECK', path, os.path.exists(path))
    if os.path.exists(path):
        conn = sqlite3.connect(path)
        cur = conn.cursor()
        print('TABLES', cur.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name").fetchall())
        rows = cur.execute("SELECT Email, UserName, IsEnabled, PasswordHash FROM AspNetUsers ORDER BY Email").fetchall()
        print('ROWS', rows)
        role_rows = cur.execute("SELECT * FROM AspNetUserRoles LIMIT 5").fetchall()
        print('ROLE_ROWS', role_rows)
        conn.close()
