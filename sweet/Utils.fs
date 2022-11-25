module Sweet.Utils

open System
open System.Threading
open System.Diagnostics

let stopProcess (id: int) =
    use proc = Process.Start ("stop.exe", string id)
    proc.WaitForExit ()

let countdown (length: TimeSpan) (intervalMs: int) tick =
    let exp = DateTime.Now + length
    let mutable over = false
    while not over do
        let now = DateTime.Now
        if now < exp then
            tick (exp - now)
            Thread.Sleep intervalMs
        else
            over <- true
