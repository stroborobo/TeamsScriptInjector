module TeamsScriptInjector.Service

open System
open System.Text.Json
open System.Text.Json.Serialization
open System.Net.Http
open System.Net.WebSockets
open System.Threading

open FSharp.Control.Tasks
open System.IO


[<CLIMutable>]
type DevToolsTarget =
    { Id: string
      Title: string
      Url: string
      WebSocketDebuggerUrl: string }

let private jsonSerializerOptions =
    let opts = JsonSerializerOptions()
    opts.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    opts.Converters.Add(JsonFSharpConverter())
    opts

let private httpClient = new HttpClient()
let getDevToolsTargetsAsync port =
    task {
        // use! stream =
        //     $"http://localhost:%d{port}/json"
        //     |> Uri
        //     |> httpClient.GetStreamAsync
        let! str =
            $"http://localhost:%d{port}/json"
            |> Uri
            |> httpClient.GetStringAsync
        // return! JsonSerializer.DeserializeAsync<DevToolsTarget[]>(stream)
        // try
            // return! JsonSerializer.DeserializeAsync<DevToolsTarget[]>(stream, jsonSerializerOptions)
        return JsonSerializer.Deserialize<DevToolsTarget[]>(str, jsonSerializerOptions)
        // with
        // | :? JsonException ->
        //     return Array.empty
    }

type private EvaluateParams =
    { Expression: string
      ObjectGroup: string
      ReturnByValue: bool
      UserGesture: bool }

type private DevToolsRequest<'TParams> =
    { Id: int
      Method: string
      Params: 'TParams }

[<CLIMutable>]
type ExceptionDetails =
    { Text: string }

[<CLIMutable>]
type DevToolsResponse =
    { ExceptionDetails: ExceptionDetails option }

let injectJavaScriptAsync javascript target =
    task {
        use client = new ClientWebSocket()
        let uri = Uri target.WebSocketDebuggerUrl
        do! client.ConnectAsync(uri, CancellationToken.None)

        let requestData =
            let msg =
                { Id = 1
                  Method = "Runtime.evaluate"
                  Params =
                    { Expression = javascript
                      ObjectGroup = "evalme"
                      ReturnByValue = false
                      UserGesture = true } }
            JsonSerializer.SerializeToUtf8Bytes(msg, jsonSerializerOptions)
            |> ArraySegment
        do! client.SendAsync(requestData, WebSocketMessageType.Text, true, CancellationToken.None)

        use memoryStream = new MemoryStream()
        let mutable isEnd = false
        while not isEnd do
            let buffer = ArraySegment<byte>(Array.zeroCreate<byte> 1024)
            let! response = client.ReceiveAsync(buffer, CancellationToken.None)
            isEnd <- response.EndOfMessage
            do! memoryStream.WriteAsync(buffer.Array, int memoryStream.Position, response.Count)
        memoryStream.Seek(0L, SeekOrigin.Begin) |> ignore
        let! response = JsonSerializer.DeserializeAsync<DevToolsResponse>(memoryStream, jsonSerializerOptions)
        match response.ExceptionDetails with
        | None -> ()
        | Some ex -> raise (Exception ex.Text)

        // seems they dont close properly on the other side, whatever
        try do! client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None)
        with _ex -> ()
    }
