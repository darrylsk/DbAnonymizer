# DbAnonymizer
Copies a SQL Server database, replacing any identifying information with anonymous or random data.
* replaces text values with random strings
* replaces numerics and dates with values close to the original but with some random variation

Uses SQL Server Management Objects to create a replica of an existing database, including tables, indexes, foreign keys, stored procedures, user-defined functions and so on.  Great for making a copy of a database complete with sample data, but with out any personal or identifying information.

This was coded as quickly as possible and partly as a learning exercise. There are lots of improvements planned (and some may even get done ;)) including:
* structure and design - the code isn't very complex, so it's not hard to understand, but could benefit from better modularity and better object re-use
* performance and memory handling
* finding a way to include some datatypes that I left out because I couldn't get the DataReader to handle them (exempli gratia, Geography and HierarchyID)
* a smarter data generation algorithm that generates more realistic-looking data
* things I forgot

Because of the performance issue, the tool is only practical for copying a small sample of database records (unless you have a lot of patience and computer memory) but that should be enough to suffice for creating a test database.

Pull requests are encouraged.  Please push your changes to a feature branch.

### Read the App.config file for important instructions on first-time use.
