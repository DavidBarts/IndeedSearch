This is a .NET console application written in C#. Do not despair, it can
be run easily on Macintosh or Linux systems if you download and install
Mono (http://www.mono-project.com/). In fact, it was developed on a Macintosh
using Mono.

------------------------------------------------------------------------

NAME

    IndeedSearch - run an Indeed.com jobs search and optionally
                   post-filter it

SYNOPSIS

    IndeedSearch [--publisher=id] [--query=string] [--location=string]
                 [--radius=integer] [--sitetype=jobsite|employer]
                 [--jobtype=fulltime|parttime|contract|internship|temporary]
                 [--daysback=integer] [--nofilter] [--latlong]
                 [--excludeagencies] [--country=string] [filter-expression]

DESCRIPTION

This runs an Indeed.com job search via Indeed's XML API, optionally post-
filters the result, and prints the results in plain text to standard
output. Why would you want to do this? Several reasons:

1. Indeed.com offers limited filtering options. Suppose you want to
   *avoid* jobs listed by a certain company. Indeed can't do that! It
   can only *limit searches to* specified firms. If you want to do the
   converse, you have to post-filter it yourself. This was in fact the
   motive for my writing this program.
2. In a metro area that has a big job market, jobs are added as you
   peruse the on-line list. That causes you to sometimes see a job twice
   (because what was job number 32 is now job number 42, the third and
   fourth result pages have repeat data). Because this program grabs
   everything almost at once, you can save the output to an unchanging
   local file and avoid this problem.
3. You can see more summary information about the job, such as the exact
   date and time (UTC) that it was posted, instead of just "3 days ago".

Options

--country=string, -c=string
    Specify the country via a two-letter country code. Default is US.
--daysback=integer, -d=integer
    Go the specified number of days back in the search, as opposed to the
    default 1.
--excludeagencies
    Attempt to exclude staffing agencies from the results.
--help, -h, -?
    Print a brief help message.
--jobtype=string -j=string
    Specify the job type to limit the search to. This must be one of
    "fulltime", "parttime", "contract", "internship", or "temporary".
    By default, all types of jobs will be returned.
--latlong
    Include approximate latitude and longitude for the employer.
--location=string, -l=string
    Specify the location of the job. This is either "City, ST" where ST
    is a state or province abbreviation, or a postal code. It is mandatory
    that a location be specified.
--nofilter
    Suppress the normal Indeed.com filtering of duplicate jobs. Note
    that is different from the post-filtering accomplished by the
    filter-expression argument(s).
--publisher=string, -p=string
    Use the specified Indeed.com publisher. This is mandatory for the
    Indeed API. If not specified, my (David Barts's) publisher code will
    be used, which should work fine.
--query=string -q=string
    Feed Indeed.com the specified query. Indeed doesn't document their
    query syntax, advising "To see what is possible, use our advanced
    search page to perform a search and then check the URL for the q
    value." It is mandatory that a query be specified.
--radius=integer, -r=integer
    Specify the radius in miles from the specified location that the
    search pertains to. Default is currently 25.
--sitetype=string, -s=string
    Specify the site type, "jobsite" or "employer". By default Indeed
    will search both.

Configuration File Support

IndeedSearch will search for a file IndeedSearch.exe.config in the same
directory that IndeedSearch.exe resides. If found, it will be treated as
a standard .NET XML configuration file. All keys in the appSettings
section of this file corresponding to a long option name above (case
insensitive) may be used to supply default values for the corresponding
option.

For example:

<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <appSettings>
    <add key="Location" value="Seattle, WA" />
    <add key="Radius" value="0" />
    <add key="query" value="(java or python)" />
    <add key="excludeAgencies" value="true" />
  </appSettings>
</configuration>

Note that values specified on the command line will override any
corresponding value in the configuration file. For flags that are either
present or absent, a string starting with "t" or "y" (case insensitive)
will be treated as Boolean true, and all other values as Boolean false.

Post-Filtering

This is probably the reason you'd want to use this program. Any arguments
following the command-line options will be concatenated (separated with
spaces) and turned into a post-filtering expression.

The post-filtering uses a .NET DataView.RowFilter expression. This is very
much like the WHERE clause in an SQL statement. For the specifics, see
http://www.csharp-examples.net/dataview-rowfilter/ .

The columns available to be filtered are the same ones whose names appear
prior to the colon character on each line of output in the job listings.
Most of these columns have string values. The exceptions are Date (a
DateTime object); Latitude and Longitude (double); and Sponsored, Expired,
and IndeedApply (bool).

For example, if you're not interested in any job at Starbucks, or any job
listed on Dice.com, you could use the following filter:
    Company <> 'Starbucks' AND Source <> 'Dice'

Note that because single quotes, <, and > have special meaning to the
shell, you should probably enclose the entire above expression in double
quotes. There is also a filterExpression key in the configuration file
that you can set to specify this value.