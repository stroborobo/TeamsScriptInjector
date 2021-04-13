module TeamsScriptInjector.State

open Elmish


type Model =
    { Port: int
      InjectedIds: string[] }

let init port =
    { Port = port
      InjectedIds = Array.empty }
    , Cmd.none

type Msg =
    | CheckDevToolsTargets
    | DevToolsTargetsLoaded of Service.DevToolsTarget []
    | Inject of Service.DevToolsTarget
    | Injected of Service.DevToolsTarget
    | Failure of exn

let update msg model =
    match msg with
    | CheckDevToolsTargets ->
        let cmd =
            Cmd.OfTask.either
                Service.getDevToolsTargetsAsync
                model.Port
                DevToolsTargetsLoaded
                Failure
        model, cmd

    | DevToolsTargetsLoaded targets ->
        let cmd =
            targets
            |> Array.filter (fun target -> target.Url.Contains "entityType=calls")
            |> Array.map (Inject >> Cmd.ofMsg)
            |> Cmd.batch
        model, cmd

    | Inject target ->
        if Array.contains target.Id model.InjectedIds then
            model, Cmd.none
        else
            let javascript = """
            setInterval(() => {
                let button = document.querySelector('[data-tid="callingAlertDismissButton_DeviceCaptureMute"]')
                if (button) { button.click() }
            }, 2000)
            """
            let cmd =
                Cmd.OfTask.either
                    (Service.injectJavaScriptAsync javascript)
                    target
                    (fun () -> Injected target)
                    Failure
            model, cmd

    | Injected target ->
        { model with
            InjectedIds = target.Id |> Array.singleton |> Array.append model.InjectedIds }
        , Cmd.none

    | Failure ex ->
        eprintfn "==> Failure! %s\n%s\n" ex.Message ex.StackTrace
        model, Cmd.none