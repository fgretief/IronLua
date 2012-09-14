#Sierra IronLua

This is a heavily modified branch of IronLua by fgretief. By "heavily modified" I mean that I am not attempting to follow any coding styles or paradigms used by the original author, rather attempting to get this branch to a usable state as quickly and efficiently as possible.

##Index
**[Current State](#current-state)**

**[Helping Out](#helping-out)**

**[Current Branch Goals](#current-branch-goals)**

**[Possible Goals](#possible-goals)**

**[Completed Goals](#completed-goals)**

**[CLR Interop Library](#clr-interop-library)**

**[IronLua](#ironlua)**

<a id="current-state"></a>
## Current State
The current state of the branch is somewhat usable, though there may well be bugs and exception generating code paths which I have not checked or fixed. Feel free to poke about and let me know if you find a problem and I'll try and get it fixed when I next have time in my schedule.

Currently it is possible to interact with a large portion of the CLR from within Lua, including accessing and writing values on both classes and structures. It is also possible to import CLR types directly from within Lua through the use of the clr namespace, from which they can then be instantiated. A number of bugs and niggles that were previously present in IronLua have also been ironed out, making it possible to do a lot more than was previously possible.

<a id="helping-out"></a>
## Helping Out
Unfortunately I don't have unlimited reserves of time to throw at this project, and it is very much a side-hobby - something I picked up because I needed a scripting language for another one of my projects and didn't feel like learning Ruby (obviously, learning the DLR was a much better option...).

If you would like to help this project out, then assistance with implementing a number of Lua's standard libraries would be appreciated. I am currently more focused on implementing the IronLua runtime and any Interop packages that are necessary than I am on getting things like Lua's debug library working.

Also, if you have knowledge on implementing stack tracing, then I'd appreciate any help you can give me there (as this would be my first attempt at doing anything of the sort).

<a id="current-branch-goals"></a>
## Current Branch Goals
Looks like I have got most of the stuff I wanted to get working done. I'll add stuff here as I come across things that need to be added or fixed. Please note that just because there isn't anything here doesn't mean that this is a finished project or that it is production ready.

<a id="possible-goals"></a>
## Possible Goals
These are things I'd like to implement eventually, however they are not at the top of my priorities list at the moment.

* **Integration into Master branch**
  If I get around to it, and this becomes a nice stable branch, then I'll try and get the changes I've made merged into the master branch.
  This won't be a priority if the original author starts making more updates which break anything I've fixed here, but if there are no changes by the time I am done
  then I'll issue a pull request.

<a id="completed-goals"></a>
## Completed Goals
These are goals which were set, and have been achieved. Some of them are small issues which needed to be fixed, while others required major rewrites of the DLR code backing IronLua. If you notice a problem with any of the issues which should have been addressed here, please put together a test method to demonstrate the issue and let me know so I can fix it.

* **Invariable Support**
  This branch should provide support for invariable values to be placed in the engine's global table which should be immutable.

* **Scoped Execution**
  This branch should provide support for execution in different scopes, allowing for engine side global variables to be set without changing their original values.
  It should also allow variables to be set and retrieved using the standard ScriptScope functions.

* **.NET Class Integration**
  It should be possible to load a .NET class into an engine to provide it for the script. 
  This will make use of the *Invariable Support* to prevent functions and other immutable properties from being changed on the class.
  It will also rely on functions which will automatically create a metatable for the class.

* **Event Support**
  Provide event handlers within Lua for C# events generated by classes (static) or by objects which are set either on the engine or scope level (instance).

* **Syntax Sugar** 
  The current implementation allows for nice, Lua-esque access to CLR object instances, however static types still need a bit of work. They currently are accessibly through the clr.* methods the same way that instanced objects are.

* **CLR Get/Set Member** 
  Current implementation works fine provided index form is not used (`obj['index']`), which currently breaks. This needs to be fixed before we can claim to have a nicely organized CLR implementation.

* **Fix Get/Set Member implementation**
  The current implementation is somewhat buggy, and does not allow you to set a field on a table using index notation if the value has not been set before, and there is no metatable handling the method. There are also a few other bugs in the implementation which need to be ironed out.

* **Event Handler Translation** 
  Current implementation doesn't work too well, since Lua methods all return a value (they behave like Func<...>) while most C# event handlers are expected to be void (Action<...>). It is necessary to get some kind of wrapper sorted out to ensure that Lua methods can be used for standard C# event handlers.

<a id="clr-interop-library"></a>
## CLR Interop Library
This implementation of Lua makes use of a library which generates Metatables for CLR objects (in a very generic way, allowing one metatable to apply to many different types at once). This has the advantage of minimizing the number of changes that need to be made to the actual Lua implementation to allow CLR interop to be possible.

###clr.import
Imports a CLR type into Lua, optionally generating the namespace tree within Lua. The imported type is technically a Metatable with information about the CLR type, and with a __call method which can instantiate that type given any constructor arguments which match.

**Example**
`math = clr.import('System.Math',false)`

###clr.call
Calls either a static CLR method (on a class), or an instance method (on an object) with the given name and arguments.

**Example**
`clr.call(math,'Pow',10,2)`

###clr.method
Gets a reference to a CLR method, which can then be treated like a function from within Lua. Basically, clr.call just skips the step of assigning this to a variable and calls it directly. There may be a small performance boost in using this over clr.call for repetitive calls.

**Example**
`clrpow = clr.method(math,'Pow')`

###clr.setvalue
Sets the value of a property or field on either a static or instance CLR object. This is required if you want to set a value on a static object, however you can make use of the standard Lua member accessors to do this on an instance object.

**Example**
`clr.setvalue(obj,'Field',value)`

###clr.getvalue
Gets the value of a property or field on either a static or instance CLR object. This is required if you want to get a value on a static object, however you can make use of the standard Lua member accessors to do this on an instance object.

**Example**
`clr.getvalue(obj,'Field')`

###clr.subscribe
Subscribes to a CLR event by supplying a Lua method which is capable of handling the event.

**Example**
`clr.subscribe(obj,'EventName',handler)`

###clr.unsubscribe
Unsubscribes from a CLR event that was previously subscribed to. It is necessary to specify the same Lua method that was previously registered.

**Example**
`clr.unsubscribe(obj,'EventName',handler)`



<a id="ironlua"></a>
# IronLua

IronLua is intended to be a full implementation of Lua targeting .NET. Allowing easy embedding into applications and friction-less integration with .NET are key goals.

It's built with C# on top of the Dynamic Language Runtime.

Licensing has not been decided upon yet but it will be some form of [permissive free software license](http://en.wikipedia.org/wiki/Permissive_free_software_licence) for easy contribution and usage without any fuss.

## A work in progress

*This is very much a work in progress project and isn't near a usable state yet.*

* 2011-06-30<br/>
  Started work on lexer.

* 2011-07-05<br/>
  Lexer has all major functionallity and can lex entire Lua. Still some bugs that will be fixed while working on parser.<br/>
  Started work on parser.

* 2011-07-17<br/>
  Can parse entire Lua. Probably have lots of minor bugs that will be fixed when I pull in the test suites.<br/>
  Have begun reading up on DLR. Will probably take some time reading documentation of the DLR before I start working on the runtime and translation of the AST to DLR expressions.

* 2011-08-09<br/>
  I have decided to rewrite the project in C#. It should be pretty straightforward to port.

* 2011-08-15<br/>
  Rewrite to C# is done. The rewrite was done for several reasons. The binary size is 4 times smaller, probably because of F#'s discriminated unions and closure's generated code among other things. Additionally tooling is alot better for C# and it is easier to reason about code performance because the IL generated is more easily mapped to C#.

* 2011-09-14<br/>
  IronLua can now generate expression trees for its entire AST. Currently working on function invokation, specifically mapping arguments to parameters. It's a quite a complex process involving type coercion and casting, expanding varargs, using parameter and type default values if not enough parameters and wrapping overflowing arguments into "params" and Varargs parameters.<br/>
  After that I will start working on all the TODO comments and get proper exception and error code everywhere. Then it's time to implement the entire Lua standard library, some parts will probably be left unimplemented like parts of the debug package and coroutines might not be implemented for the 0.1.0 release. Finally I will create the test harness and hopefully find some useable test code I can bring in. And that's pretty much it for the 0.1.0 release. Full .NET integration and proper error messages/stack traces is slated for 0.2.0 and possibly 0.3.0.