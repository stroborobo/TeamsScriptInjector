open System.Text.Json

#load ".paket/load/main.group.fsx"
#load "Service.fs"

let json = """
{"exceptionDetails": {"text": "kaputt"}}
"""

let options = JsonSerializerOptions()
options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase

JsonSerializer.Deserialize<TeamsScriptInjector.Service.DevToolsResponse>(json, options)
