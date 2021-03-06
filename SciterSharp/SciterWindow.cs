﻿// Copyright 2016 Ramon F. Mendes
//
// This file is part of SciterSharp.
// 
// SciterSharp is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// SciterSharp is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with SciterSharp.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SciterSharp.Interop;
#if OSX
using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;
#endif

namespace SciterSharp
{
	public class SciterWindow
	{
		protected static SciterX.ISciterAPI _api = SciterX.API;
		public IntPtr _hwnd { get; set; }
		private SciterXDef.FPTR_SciterWindowDelegate _proc;
#if GTKMONO
		public IntPtr _gtkwindow { get; private set; }
#elif OSX
		public NSView _nsview { get; private set; }
#endif

		public SciterWindow()
		{
#if WINDOWS
			_proc = InternalProcessSciterWindowMessage;
#else
			_proc = null;
#endif
		}

		public const SciterXDef.SCITER_CREATE_WINDOW_FLAGS DefaultCreateFlags =
			SciterXDef.SCITER_CREATE_WINDOW_FLAGS.SW_MAIN |
			SciterXDef.SCITER_CREATE_WINDOW_FLAGS.SW_TITLEBAR |
			SciterXDef.SCITER_CREATE_WINDOW_FLAGS.SW_RESIZEABLE |
			SciterXDef.SCITER_CREATE_WINDOW_FLAGS.SW_CONTROLS |
			SciterXDef.SCITER_CREATE_WINDOW_FLAGS.SW_ENABLE_DEBUG;

		/// <summary>
		/// Creates the Sciter window and returns the native handle
		/// </summary>
		/// <param name="frame">Rectangle of the window</param>
		/// <param name="creationFlags">Flags for the window creation, defaults to SW_MAIN | SW_TITLEBAR | SW_RESIZEABLE | SW_CONTROLS | SW_ENABLE_DEBUG</param>
		public void CreateWindow(PInvokeUtils.RECT frame = new PInvokeUtils.RECT(), SciterXDef.SCITER_CREATE_WINDOW_FLAGS creationFlags = DefaultCreateFlags, IntPtr parent = new IntPtr())
		{
			_hwnd = _api.SciterCreateWindow(
				creationFlags,
				ref frame,
				_proc,
				IntPtr.Zero,
				parent
			);
			Debug.Assert(_hwnd != IntPtr.Zero);

			if(_hwnd == IntPtr.Zero)
				throw new Exception("CreateWindow() failed");

#if GTKMONO
			_gtkwindow = PInvokeGTK.gtk_widget_get_toplevel(_hwnd);
			Debug.Assert(_gtkwindow != IntPtr.Zero);
#elif OSX
			_nsview = new NSView(_hwnd);
#endif
		}

		public void CreateMainWindow(int width, int height, SciterXDef.SCITER_CREATE_WINDOW_FLAGS creationFlags = DefaultCreateFlags)
		{
			PInvokeUtils.RECT frame = new PInvokeUtils.RECT();
			frame.right = width;
			frame.bottom = height;
			CreateWindow(frame, creationFlags);
		}

		public void CreatePopupAlphaWindow(int width, int height, IntPtr owner_hwnd)
		{
			PInvokeUtils.RECT frame = new PInvokeUtils.RECT();
			frame.right = width;
			frame.bottom = height;
			CreateWindow(frame, SciterXDef.SCITER_CREATE_WINDOW_FLAGS.SW_ALPHA | SciterXDef.SCITER_CREATE_WINDOW_FLAGS.SW_TOOL, owner_hwnd);
			// Sciter BUG: window comes with WM_EX_APPWINDOW style
		}

#if WINDOWS
		public void CreateChildWindow(IntPtr hwnd_parent)
		{
			if(PInvokeWindows.IsWindow(hwnd_parent) == false) throw new ArgumentException("Invalid parent window");

			PInvokeUtils.RECT rc;
			PInvokeWindows.GetClientRect(hwnd_parent, out rc);

			string wndclass = Marshal.PtrToStringUni(_api.SciterClassName());
			_hwnd = PInvokeWindows.CreateWindowEx(0, wndclass, null, PInvokeWindows.WS_CHILD, 0, 0, rc.right, rc.bottom, hwnd_parent, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

			/*PInvokeUtils.RECT frame = new PInvokeUtils.RECT();
			_hwnd = _api.SciterCreateWindow(SciterXDef.SCITER_CREATE_WINDOW_FLAGS.SW_CHILD, ref frame, _proc, IntPtr.Zero, hwnd_parent);
			if(_hwnd == IntPtr.Zero)
				throw new Exception("CreateChildWindow() failed");*/
		}
#endif

		public void Destroy()
		{
#if WINDOWS
			PInvokeWindows.DestroyWindow(_hwnd);
#endif
#if GTKMONO
			PInvokeGTK.gtk_widget_destroy(_gtkwindow);
#endif
		}

		/// <summary>
		/// Centers the window in the screen. You must call it after the window is created, but before it is shown to avoid flickering
		/// </summary>
		public void CenterTopLevelWindow()
		{
#if WINDOWS
			IntPtr hwndParent = PInvokeWindows.GetDesktopWindow();
			PInvokeUtils.RECT rectWindow, rectParent;

			PInvokeWindows.GetWindowRect(_hwnd, out rectWindow);
			PInvokeWindows.GetWindowRect(hwndParent, out rectParent);

			int nWidth = rectWindow.right - rectWindow.left;
			int nHeight = rectWindow.bottom - rectWindow.top;

			int nX = ((rectParent.right - rectParent.left) - nWidth) / 2 + rectParent.left;
			int nY = ((rectParent.bottom - rectParent.top) - nHeight) / 2 + rectParent.top;

			int nScreenWidth = PInvokeWindows.GetSystemMetrics(PInvokeWindows.SystemMetric.SM_CXSCREEN);
			int nScreenHeight = PInvokeWindows.GetSystemMetrics(PInvokeWindows.SystemMetric.SM_CYSCREEN);

			if (nX < 0) nX = 0;
			if (nY < 0) nY = 0;
			if (nX + nWidth > nScreenWidth) nX = nScreenWidth - nWidth;
			if (nY + nHeight > nScreenHeight) nY = nScreenHeight - nHeight;

			PInvokeWindows.MoveWindow(_hwnd, nX, nY, nWidth, nHeight, false);
#elif GTKMONO
			int screen_width = PInvokeGTK.gdk_screen_width();
			int screen_height = PInvokeGTK.gdk_screen_height();

			int window_width, window_height;
			PInvokeGTK.gtk_window_get_size(_gtkwindow, out window_width, out window_height);

			int nX = (screen_width - window_width) / 2;
			int nY = (screen_height - window_height) / 2;

			PInvokeGTK.gtk_window_move(_gtkwindow, nX, nY);
#elif OSX
			_nsview.Window.Center();
#endif
		}

		/// <summary>
		/// Cross-platform handy method to get the size of the screen
		/// </summary>
		/// <returns>SIZE measures of the screen of primary monitor</returns>
		public static PInvokeUtils.SIZE GetPrimaryMonitorScreenSize()
		{
#if WINDOWS
			int nScreenWidth = PInvokeWindows.GetSystemMetrics(PInvokeWindows.SystemMetric.SM_CXSCREEN);
			int nScreenHeight = PInvokeWindows.GetSystemMetrics(PInvokeWindows.SystemMetric.SM_CYSCREEN);
			return new PInvokeUtils.SIZE() { cx = nScreenWidth, cy = nScreenHeight };
#elif GTKMONO
			int screen_width = PInvokeGTK.gdk_screen_width();
			int screen_height = PInvokeGTK.gdk_screen_height();
			return new PInvokeUtils.SIZE() { cx = screen_width, cy = screen_height };
#elif OSX
			return new PInvokeUtils.SIZE();// TODO
#endif
		}

		/// <summary>
		/// Loads the page resource from the given URL or file path
		/// </summary>
		/// <param name="url_or_filepath">URL or file path of the page</param>
		public bool LoadPage(string url_or_filepath)
		{
			return _api.SciterLoadFile(_hwnd, url_or_filepath);
		}

		/// <summary>
		/// Loads HTML input from a string
		/// </summary>
		/// <param name="html">HTML of the page to be loaded</param>
		/// <param name="baseUrl">Base Url given to the loaded page</param>
		public bool LoadHtml(string html, string baseUrl = null)
		{
			var bytes = Encoding.UTF8.GetBytes(html);
			return _api.SciterLoadHtml(_hwnd, bytes, (uint) bytes.Length, baseUrl);
		}

		public void Show(bool show = true)
		{
#if WINDOWS
			PInvokeWindows.ShowWindow(_hwnd, show ? PInvokeWindows.ShowWindowCommands.Show : PInvokeWindows.ShowWindowCommands.Hide);
#elif GTKMONO
			if(show)
				PInvokeGTK.gtk_window_present(_gtkwindow);
			else
				PInvokeGTK.gtk_widget_hide(_hwnd);
#elif OSX
			if(show)
			{
				_nsview.Window.MakeMainWindow();
				_nsview.Window.MakeKeyWindow();
				_nsview.Window.MakeKeyAndOrderFront(null);
			} else {
				_nsview.Window.Miniaturize(_nsview.Window);// PerformMiniaturize?
			}
#endif
		}

		public void Close()
		{
#if WINDOWS
			PInvokeWindows.PostMessage(_hwnd, PInvokeWindows.Win32Msg.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
#elif GTKMONO
			PInvokeGTK.gtk_window_close(_gtkwindow);
#elif OSX
			_nsview.Window.Close();
#endif
		}


		public Icon Icon
		{
			set
			{
#if WINDOWS
				PInvokeWindows.SendMessageW(_hwnd, PInvokeWindows.Win32Msg.WM_SETICON, IntPtr.Zero, value.Handle);
#endif
			}
		}

		public string Title
		{
			set
			{
				Debug.Assert(_hwnd!=IntPtr.Zero);
#if WINDOWS
				IntPtr strPtr = Marshal.StringToHGlobalUni(value);
				PInvokeWindows.SendMessageW(_hwnd, PInvokeWindows.Win32Msg.WM_SETTEXT, IntPtr.Zero, strPtr);
				Marshal.FreeHGlobal(strPtr);
#elif GTKMONO
				PInvokeGTK.gtk_window_set_title(_gtkwindow, value);
#elif OSX
				_nsview.Window.Title = value;
#endif
			}

			get
			{
				Debug.Assert(_hwnd!=IntPtr.Zero);
#if WINDOWS
				IntPtr unmanagedPointer = Marshal.AllocHGlobal(2048);
				IntPtr chars_copied = PInvokeWindows.SendMessageW(_hwnd, PInvokeWindows.Win32Msg.WM_GETTEXT, new IntPtr(2048), unmanagedPointer);
				string title = Marshal.PtrToStringUni(unmanagedPointer, chars_copied.ToInt32());
				Marshal.FreeHGlobal(unmanagedPointer);
				return title;
#elif GTKMONO
				IntPtr str_ptr = PInvokeGTK.gtk_window_get_title(_gtkwindow);
				return Marshal.PtrToStringAnsi(str_ptr);
#elif OSX
				return _nsview.Window.Title;
#endif
			}
		}
		
		public SciterElement RootElement
		{
			get
			{
				Debug.Assert(_hwnd != IntPtr.Zero);
				IntPtr he;
				_api.SciterGetRootElement(_hwnd, out he);
				return new SciterElement(he);
			}
		}

		/// <summary>
		/// Find element at point x/y of the window, client area relative
		/// </summary>
		public SciterElement ElementAtPoint(int x, int y)
		{
			PInvokeUtils.POINT pt = new PInvokeUtils.POINT() { X = x, Y = y };
			IntPtr outhe;
			_api.SciterFindElement(_hwnd, pt, out outhe);

			if(outhe == IntPtr.Zero)
				return null;
			return new SciterElement(outhe);
		}

		public uint GetMinWidth()
		{
			Debug.Assert(_hwnd != IntPtr.Zero);
			return _api.SciterGetMinWidth(_hwnd);
		}

		public uint GetMinHeight(uint for_width)
		{
			Debug.Assert(_hwnd != IntPtr.Zero);
			return _api.SciterGetMinHeight(_hwnd, for_width);
		}

		/// <summary>
		/// Update pending changes in Sciter window and forces painting if necessary
		/// </summary>
		public void UpdateWindow()
		{
			_api.SciterUpdateWindow(_hwnd);
		}

		/// <summary>
		/// For example media type can be "handheld", "projection", "screen", "screen-hires", etc.
		/// By default sciter window has "screen" media type.
		/// Media type name is used while loading and parsing style sheets in the engine so
		/// you should call this function* before* loading document in it.
		/// </summary>
		public bool SetMediaType(string mediaType)
		{
			return _api.SciterSetMediaType(_hwnd, mediaType);
		}

		/// <summary>
		/// For example media type can be "handheld:true", "projection:true", "screen:true", etc.
		/// By default sciter window has "screen:true" and "desktop:true"/"handheld:true" media variables.
		/// Media variables can be changed in runtime. This will cause styles of the document to be reset.
		/// </summary>
		/// <param name="mediaVars">Map that contains name/value pairs - media variables to be set</param>
		public bool SetMediaVars(SciterValue mediaVars)
		{
			SciterXValue.VALUE v = mediaVars.ToVALUE();
			return _api.SciterSetMediaVars(_hwnd, ref v);
		}

#if WINDOWS
		private IntPtr InternalProcessSciterWindowMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, IntPtr pParam, ref bool handled)
		{
			Debug.Assert(pParam.ToInt32()==0);
			Debug.Assert(_hwnd.ToInt32()==0 || hwnd==_hwnd);
			
			IntPtr lResult = IntPtr.Zero;
			handled = ProcessWindowMessage(hwnd, msg, wParam, lParam, ref lResult);
			return lResult;
		}

		protected virtual bool ProcessWindowMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, ref IntPtr lResult)// overrisable
		{
			return false;
		}
#endif
	}
}