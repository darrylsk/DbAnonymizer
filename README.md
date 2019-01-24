# DbAnonymizer
Copies a SQL Server database, replacing any identifying information with anonymous or random data.

Uses SQL Server Management Objects to create a replica of an existing database, including indexes, user-defined functions and foreign keys.  Other objects such as ~stored procedures~ (now implemented :-) and triggers are not included yet, but are planned and hopefully will follow soon.

There are lots of improvements that could be made, including:
* structure and design - this was coded as quickly as possible and partly as a learning exercise, so some refactoring would definitely help
* performance and memory hanling
* some datatypes that are difficult for the DataReader (exempli gratia, Geography and HierarchyID) that I've left out
* things I forgot

Because of the performance issue, the tool is only practical for copying a small sample of database records (unless you have a lot of patience and computer memory) but this should suffice for a test database, which is it's only purpose.

Pull requests are encouraged.  Please push your changes to a feature branch.

### Read the App.config file for important instructions on first-time use.
