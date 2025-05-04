using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WindowsInput;

namespace OBZBarcodeTray;

internal class FrmTray : Form
{
    private readonly NotifyIcon notifyIcon;

    //private readonly ToolStripMenuItem pair;
    private readonly ToolStripMenuItem pause;
    private readonly ToolStripMenuItem exit;

    private readonly InputSimulator inputSimulator;

    private HubConnection connection;

    internal FrmTray()
    {
        inputSimulator = new InputSimulator();

        //pair = new ToolStripMenuItem("Poveži", null, new EventHandler(Pair), "Poveži");
        pause = new ToolStripMenuItem("Pauziraj", null, new EventHandler(Pause), "Pauziraj");
        exit = new ToolStripMenuItem("Izlaz", null, new EventHandler(Exit), "Izlaz");


        notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Warning,
            ContextMenuStrip = new ContextMenuStrip(),
            Visible = true
        };
        notifyIcon.ContextMenuStrip.Items.AddRange(new ToolStripItem[] { pause, exit });
    }
}
