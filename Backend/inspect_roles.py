import sqlite3
from pathlib import Path

path = Path("ExpenseTracker.API/expensetracker.db")
if not path.exists():
    raise FileNotFoundError(path)

conn = sqlite3.connect(path)
cur = conn.cursor()
for title, query in [
    ("Users", "SELECT Id, UserName, Email FROM AspNetUsers"),
    ("Roles", "SELECT Id, Name, NormalizedName FROM AspNetRoles"),
    ("UserRoles", "SELECT UserId, RoleId FROM AspNetUserRoles"),
    ("RoleClaims", "SELECT Id, RoleId, ClaimType, ClaimValue FROM AspNetRoleClaims"),
    ("UserClaims", "SELECT Id, UserId, ClaimType, ClaimValue FROM AspNetUserClaims"),
]:
    print(title)
    for row in cur.execute(query).fetchall():
        print(row)
    print()
conn.close()
