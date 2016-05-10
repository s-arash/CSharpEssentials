# C# Essentials

C# Essentials is a collection of Roslyn diagnostic analyzers, code fixes and
refactorings that make it easy to work with C# 6 language features,
such as [nameof expressions](https://github.com/dotnet/roslyn/wiki/New-Language-Features-in-C%23-6#nameof-expressions),
[getter-only auto-properties](https://github.com/dotnet/roslyn/wiki/New-Language-Features-in-C%23-6#getter-only-auto-properties),
[expression-bodied members](https://github.com/dotnet/roslyn/wiki/New-Language-Features-in-C%23-6#expression-bodied-function-members),
 [string interpolation](https://github.com/dotnet/roslyn/wiki/New-Language-Features-in-C%23-6#string-interpolation), and [null-conditional operators](https://github.com/dotnet/roslyn/wiki/New-Language-Features-in-C%23-6#null-conditional-operators).

Supports Visual Studio 2015 ([link](https://visualstudiogallery.msdn.microsoft.com/a4445ad0-f97c-41f9-a148-eae225dcc8a5?SRC=Home))

## Features

### Use NameOf

Identifies calls where a parameter name is passed as a string to an argument
named "paramName". This is a simple-yet-effective heuristic for detecting
cases like the one below:

![](http://i.imgur.com/JnNB8nZ.jpg)

### Use Getter-Only Auto-Property

Determines when the ```private set``` in an auto-property can be removed.

![](http://i.imgur.com/je8HpdD.jpg)

### Use Expression-Bodied Member

Makes it clear when a member can be converted into an expression-bodied
member.

![](http://i.imgur.com/vF4PY9o.jpg)

### Expand Expression-Bodied Member

Makes it trivial to convert an expression-bodied member into a full member
declaration with a body (and a get accessor declaration for properties and
indexers).

![](http://i.imgur.com/WROjVdP.jpg)

### Convert to Interpolated String

This handy refactoring makes it a breeze to transform a ```String.Format```
call into an interpolated strings.

![](http://i.imgur.com/Q1CMKD5.jpg)

### Use Null-Conditional Operators

Identifies when invocations guarded with null-check if statements can be simplfied using null-conditional operators.

![](http://i.imgur.com/8YhAnfM.png)