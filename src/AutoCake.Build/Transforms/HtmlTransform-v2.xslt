<?xml version="1.0" encoding="UTF-8"?>

<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output omit-xml-declaration="yes" indent="yes" />
  <xsl:template match="/">
    <html>
      <head>
        <style type="text/css">
          th {
          background-color: #A0A0A0;
          border-bottom: 1pt solid black;
          padding: 0 4pt;
          }

          td {
          border-bottom: 1pt solid black;
          padding: 0 4pt;
          }

        </style>
      </head>
      <body>
        <xsl:apply-templates />
      </body>
    </html>
  </xsl:template>

  <xsl:template match="test-results">
    <h1>
      <xsl:value-of select="@name" />
    </h1>
    <div>
      NUnit Version: <xsl:value-of select="environment/@nunit-version" /> <br />
      Date: <xsl:value-of select="@date" />  <br />
      Time: <xsl:value-of select="@time" />  <br />
      Runtime Environment: <xsl:value-of select="environment/@os-version" /> <br />
      CLR Version: <xsl:value-of select="environment/@clr-version" /> <br />
    </div>

    <div>
      <p>
        Tests run:     <xsl:value-of select="@total" />,
        <xsl:choose>
          <xsl:when test="substring(environment/@nunit-version,1,3)>='2.5'">
            Errors: <xsl:value-of select="@errors" />,
            Failures: <xsl:value-of select="@failures" />
            <xsl:if test="@inconclusive">
              <!-- Introduced in 2.5.1 -->
              , Inconclusive: <xsl:value-of select="@inconclusive" />
            </xsl:if>
          </xsl:when>
          <xsl:otherwise>
            Failures: <xsl:value-of select="@failures" />,
            Not run: <xsl:value-of select="@not-run" />
          </xsl:otherwise>
        </xsl:choose>
        ,
        Time: <xsl:value-of select="test-suite/@time" /> seconds.
        <xsl:if test="substring(environment/@nunit-version,1,3)>='2.5'">
          (Not run: <xsl:value-of select="@not-run" />,
          Invalid: <xsl:value-of select="@invalid" />,
          Ignored: <xsl:value-of select="@ignored" />,
          Skipped: <xsl:value-of select="@skipped" /> )
        </xsl:if>
      </p>
    </div>

    <xsl:if test="//test-case[failure]">
      <h4>Failures:</h4>
      <table>
        <thead>
          <th>Name</th>
          <th>Result</th>
          <th>Time</th>
          <th>Asserts</th>
          <th>Category</th>
          <th>Properties</th>
        </thead>
        <tbody>
          <xsl:apply-templates select="//test-case[failure]" />
        </tbody>
      </table>
      <hr />
    </xsl:if>

    <xsl:if test="//test-case[@executed='False']">
      <h4>Tests not run:</h4>
      <table class="results">
        <thead>
          <th>Name</th>
          <th>Result</th>
          <th>Time</th>
          <th>Asserts</th>
          <th>Category</th>
          <th>Properties</th>
        </thead>
        <tbody>
          <xsl:apply-templates select="//test-case[@executed='False']" />
        </tbody>
      </table>
      <hr />
    </xsl:if>

    <table>
      <thead>
        <th>Name</th>
        <th>Result</th>
        <th>Time</th>
        <th>Asserts</th>
        <th>Category</th>
        <th>Properties</th>
      </thead>
      <tbody>
        <xsl:apply-templates select="//test-case" />
      </tbody>
    </table>
  </xsl:template>

  <xsl:template match="test-case">
    <tr>
      <td>
        <xsl:value-of select="@name" />
      </td>
      <td>
        <xsl:value-of select="@result" />
      </td>
      <td>
        <xsl:value-of select="@time" />
      </td>
      <td>
        <xsl:value-of select="@asserts" />
      </td>
      <td>
        <xsl:apply-templates select="categories/category" />
      </td>
      <td>
        <xsl:apply-templates select="properties/property" />
      </td>
    </tr>
  </xsl:template>

  <xsl:template match="test-case//category">
    <xsl:value-of select="@name" />
    <xsl:if test="position() != last()">
      <xsl:text>, </xsl:text>
    </xsl:if>
  </xsl:template>

  <xsl:template match="test-case//property">
    <xsl:value-of select="@name" />=<xsl:value-of select="@value" />
    <xsl:if test="position() != last()">
      <xsl:text>, </xsl:text>
    </xsl:if>
  </xsl:template>

</xsl:stylesheet>