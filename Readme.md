# Veeam

Implementation of test task.

---

## 📦 Requirements

- .NET (recommended .NET 6 or newer)
- NLog package

Install NLog:

```sh
dotnet add package NLog
```


Usage:
Run the program from the command line:

```nginx
dotnet run -- "E:\Veeam\source" "E:\Veeam\replica" 5 "E:\Veeam\logs.json"
```

## How It Works

### 🔹 Startup
- Validates arguments  
- Initializes NLog (console + log file)  
- Creates replica directory if not present  
- Registers Ctrl+C handler  

---

### 🔹 Sync Loop  
Repeated every `<intervalSeconds>`:

#### ✔ Creates directories
Replicates the entire directory structure from the source.

#### ✔ Copies and updates files
- New files are created  
- Modified files are updated using atomic copy  
- Timestamp preserved (UTC)

#### ✔ Deletes missing files
Any file or directory that does not exist in the source is removed from the replica.

---

### 🔹 Shutdown
Press **Ctrl+C** → Sync stops gracefully → LogManager shuts down



