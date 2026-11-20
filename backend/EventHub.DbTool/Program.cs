using System.Text.Json;
using System.Text.RegularExpressions;
using EventHub;
using Microsoft.Extensions.Configuration;
using Npgsql;

// Local verification helper. Credentials arrive on stdin, never in command arguments.
try {
    var data=JsonDocument.Parse(await Console.In.ReadToEndAsync()).RootElement;
    var input=data.GetProperty("connection").GetString()!;
    var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"DATABASE_URL",input}}).Build();
    var b=new NpgsqlConnectionStringBuilder(DatabaseConfiguration.PostgresConnection(config));
    var action=data.GetProperty("action").GetString();
    var database=data.TryGetProperty("database",out var d)?d.GetString():null;
    if(action is "create" or "drop" or "query") {
        if(database==null||!Regex.IsMatch(database,"^eventhub_test_[a-f0-9]{16}$"))throw new InvalidOperationException("Invalid isolated fixture database.");
    }
    if(action=="query")b.Database=database;
    await using var connection=new NpgsqlConnection(b.ConnectionString);
    await connection.OpenAsync();
    var sql=action switch {
        "create"=>$"CREATE DATABASE \"{database}\"",
        "drop"=>$"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)",
        "query"=>data.GetProperty("query").GetString()!,
        _=>throw new InvalidOperationException("Unknown verification action.")
    };
    await using var command=new NpgsqlCommand(sql,connection);
    await using var reader=await command.ExecuteReaderAsync();
    var rows=new List<string[]>();
    while(await reader.ReadAsync()) {
        var row=new string[reader.FieldCount];
        for(var i=0;i<row.Length;i++)row[i]=reader.IsDBNull(i)?"":Convert.ToString(reader.GetValue(i),System.Globalization.CultureInfo.InvariantCulture)!;
        rows.Add(row);
    }
    Console.Write(Json.Encode(new{ok=true,rows}));
}catch(Exception ex){Console.Write(Json.Encode(new{ok=false,error=ex.GetType().Name,code=(ex as PostgresException)?.SqlState,detail=ex.GetBaseException().Message}));Environment.ExitCode=1;}
