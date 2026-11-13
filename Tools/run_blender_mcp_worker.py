"""Run with Blender -b --factory-startup --python Tools/run_blender_mcp_worker.py."""
import importlib.util
import os
from pathlib import Path
import socket
import sys
import threading
import time

import bpy

version = ".".join(map(str, bpy.app.version[:2]))
addon_path = Path(os.environ["APPDATA"]) / "Blender Foundation" / "Blender" / version / "scripts/addons/blender_mcp.py"
spec = importlib.util.spec_from_file_location("blender_mcp", addon_path)
addon = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = addon
spec.loader.exec_module(addon)

for library in ("polyhaven", "hyper3d", "sketchfab", "polypizza", "hunyuan3d"):
    setattr(bpy.types.Scene, "blendermcp_use_" + library, bpy.props.BoolProperty(default=False))

server = addon.BlenderMCPServer(host="127.0.0.1", port=9877)
with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as listener:
    listener.bind((server.host, server.port))
    listener.listen(5)
    server.socket = listener
    server.running = True
    server.server_thread = threading.Thread(target=server._server_loop, daemon=True)
    server.server_thread.start()
    print("Blender MCP worker listening on 127.0.0.1:9877", flush=True)
    try:
        # A background Blender process has no GUI timer loop. Drain on its main thread.
        while server.running:
            server._drain_command_queue()
            time.sleep(.05)
    finally:
        server.running = False
