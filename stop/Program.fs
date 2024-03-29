﻿open System
open System.Runtime.InteropServices
open System.Diagnostics

[<DllImport ("kernel32.dll", SetLastError = true, ExactSpelling = true)>]
extern bool FreeConsole ()

[<DllImport ("kernel32.dll", SetLastError = true)>]
extern bool AttachConsole (uint32 dwProcessId)

[<DllImport "kernel32.dll">]
extern bool GenerateConsoleCtrlEvent (uint32 dwCtrlEvent, uint32 dwProcessGroupId)

type ConsoleCtrlDelegate = delegate of uint32 -> bool
[<DllImport "kernel32.dll">]
extern bool SetConsoleCtrlHandler (ConsoleCtrlDelegate handlerRoutine, bool add)

type StopError =
    | ProcessIdNotFound
    | UnexpectedError of string

let stop logProcessName processId =
    let proc =
        try Process.GetProcessById processId |> Ok
        with :? ArgumentException -> Error ProcessIdNotFound
    match proc with
    | Error msg -> Error msg
    | Ok proc ->
        try
            logProcessName proc.ProcessName
            FreeConsole () |> ignore
            AttachConsole (uint32 processId) |> ignore
            SetConsoleCtrlHandler (null, true) |> ignore
            GenerateConsoleCtrlEvent (0u, 0u) |> ignore
            proc.WaitForExit ()
            SetConsoleCtrlHandler (null, false) |> ignore
            Ok ()
        with ex -> Error (UnexpectedError ex.Message)

let parseProcessId (argv: string[]) =
    if argv.Length <> 1 then None
    else
        match Int32.TryParse argv.[0] with
        | true, processId -> Some processId
        | _ -> None

[<EntryPoint>]
let main argv =
    match parseProcessId argv with
    | None ->
        printfn "Usage: stop.exe <processId>"
        -1
    | Some processId ->
        let result =
            processId
            |> stop (fun processName -> printfn "Stopping process %s:%d..." processName processId)
        match result with
        | Error ProcessIdNotFound ->
            printfn "Process with ID %d not found" processId
            -2
        | Error (UnexpectedError msg) ->
            printfn "%s" msg
            -3
        | Ok () ->
            0
