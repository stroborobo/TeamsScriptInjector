open System
open System.Diagnostics
open System.Threading
open Elmish

open TeamsScriptInjector.State

type Args =
    { Port: int
      ExecutablePath: string }

let run args =
    use proc =
        let startInfo = ProcessStartInfo(args.ExecutablePath, $"--remote-debugging-port={args.Port}", CreateNoWindow = true)
        new Process(StartInfo = startInfo)
    if proc.Start() then ()
    else eprintfn "process already running: %s" args.ExecutablePath

    let mutable timer: Timer = null
    let subCmd _ =
        let sub dispatch =
            let interval = TimeSpan.FromSeconds(5.)
            timer <- new Timer(
                TimerCallback (fun _ -> dispatch CheckDevToolsTargets),
                null,
                interval,
                interval)
        Cmd.ofSub sub

    Program.mkProgram init update (fun _ _ -> ())
    |> Program.withSubscription subCmd
    // |> Program.withConsoleTrace
    |> Program.runWith args.Port

    proc.WaitForExit()

    if not (isNull timer) then timer.Dispose()

    ()

[<EntryPoint>]
let main _argv =
    { ExecutablePath = "/Applications/Microsoft Teams.app/Contents/MacOS/Teams"
      Port = 12345 }
    |> run

    0