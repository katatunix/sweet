open System.Runtime.InteropServices
open System.Diagnostics

[<DllImport ("kernel32.dll", SetLastError = true, ExactSpelling = true)>]
extern bool FreeConsole ()

[<DllImport ("kernel32.dll", SetLastError = true)>]
extern bool AttachConsole (uint32 dwProcessId)

[<DllImport "kernel32.dll">]
extern bool GenerateConsoleCtrlEvent (uint32 dwCtrlEvent, uint32 dwProcessGroupId)

let freeConsole () =
    try let ok = FreeConsole ()
        if ok then Ok () else Error "Could not FreeConsole"
    with ex ->
        Error ("Could not FreeConsole: " + ex.Message)

let attachConsole processId =
    try let ok = AttachConsole (uint32 processId)
        if ok then Ok () else Error "Could not AttachConsole"
    with ex ->
        Error ("Could not AttachConsole: " + ex.Message)

let generateConsoleCtrlEvent ctrlEvent processGroupId =
    try let ok = GenerateConsoleCtrlEvent (ctrlEvent, processGroupId)
        if ok then Ok () else Error "Could not GenerateConsoleCtrlEvent"
    with ex ->
        Error ("Could not GenerateConsoleCtrlEvent: " + ex.Message)

let getProcessName id =
    try use proc = Process.GetProcessById id
        Ok proc.ProcessName
    with ex ->
        Error (sprintf "Process with Id %d not found" id)

let stop processId =
    getProcessName processId
    |> Result.bind (fun processName ->
        printfn "Stopping %s:%d..." processName processId
        freeConsole ()
    )
    |> Result.bind (fun _ ->
        attachConsole processId
    )
    |> Result.bind (fun _ ->
        generateConsoleCtrlEvent 0u 0u
    )

let parseProcessId (argv: string[]) =
    if argv.Length = 0 then None
    else
        match System.Int32.TryParse argv.[0] with
        | true, processId -> Some processId
        | _ -> None

let usage () =
    printfn "Usage: Stop.exe processId"

[<EntryPoint>]
let main argv =
    match parseProcessId argv with
    | None ->
        usage ()
        -1
    | Some processId ->
        match stop processId with
        | Error msg ->
            printfn "%s" msg
            -2
        | Ok () ->
            0
