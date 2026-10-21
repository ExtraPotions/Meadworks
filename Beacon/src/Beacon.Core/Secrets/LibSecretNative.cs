// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Public non-variadic libsecret/GLib/GIO APIs only. Invoked exclusively by LinuxKeyringStore's background tasks.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace ExtraPotions.Beacon.Secrets
{
    internal static class LibSecretNative
    {
        const string Secret = "libsecret-1.so.0", Glib = "libglib-2.0.so.0", Gio = "libgio-2.0.so.0", Gobject = "libgobject-2.0.so.0";
        const int SearchAll = 2;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint Hash(IntPtr text);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Equal(IntPtr a, IntPtr b);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Free(IntPtr value);
        static readonly Hash hash = g_str_hash;
        static readonly Equal equal = g_str_equal;
        static readonly Free free = g_free;

        internal static bool Store(string reference, string password, CancellationToken token)
        {
            using var call = new Call(reference, token);
            var result = secret_password_storev_sync(call.Schema, call.Attributes, "default", "Meadworks Beacon server password", password, call.Cancellable, out var error);
            Check(error, token);
            return result != 0;
        }
        internal static SecretReadResult Read(string reference, CancellationToken token)
        {
            using var call = new Call(reference, token);
            var value = secret_password_lookupv_sync(call.Schema, call.Attributes, call.Cancellable, out var error);
            try
            {
                Check(error, token);
                return value == IntPtr.Zero ? default : new SecretReadResult(true, Marshal.PtrToStringUTF8(value) ?? "");
            }
            finally { if (value != IntPtr.Zero) secret_password_free(value); }
        }
        internal static bool Forget(string reference, CancellationToken token)
        {
            using var call = new Call(reference, token);
            var result = secret_password_clearv_sync(call.Schema, call.Attributes, call.Cancellable, out var error);
            Check(error, token);
            return result != 0;
        }
        internal static HashSet<string> References(CancellationToken token)
        {
            using var call = new Call(null, token);
            // ALL, without UNLOCK or LOAD_SECRETS: presence indicators never fetch password values.
            var list = secret_password_searchv_sync(call.Schema, call.Attributes, SearchAll, call.Cancellable, out var error);
            var found = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                Check(error, token);
                for (var node = list; node != IntPtr.Zero;)
                {
                    var item = Marshal.PtrToStructure<ListNode>(node);
                    var attributes = secret_retrievable_get_attributes(item.Data);
                    try
                    {
                        var reference = Marshal.PtrToStringUTF8(g_hash_table_lookup(attributes, "ref"));
                        if (SecretReference.IsValid(reference)) found.Add(reference!);
                    }
                    finally { if (attributes != IntPtr.Zero) g_hash_table_unref(attributes); }
                    node = item.Next;
                }
                return found;
            }
            finally
            {
                for (var node = list; node != IntPtr.Zero;)
                {
                    var item = Marshal.PtrToStructure<ListNode>(node);
                    g_object_unref(item.Data); node = item.Next;
                }
                if (list != IntPtr.Zero) g_list_free(list);
            }
        }
        static void Check(IntPtr error, CancellationToken token)
        {
            if (error != IntPtr.Zero) g_error_free(error); // Never marshal or report a native error message.
            token.ThrowIfCancellationRequested();
            if (error != IntPtr.Zero) throw new KeyringUnavailableException();
        }
        [StructLayout(LayoutKind.Sequential)] struct ListNode { internal IntPtr Data, Next, Previous; }

        sealed class Call : IDisposable
        {
            internal IntPtr Schema, Attributes, Cancellable;
            CancellationTokenRegistration registration;
            internal Call(string? reference, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var types = g_hash_table_new_full(hash, equal, free, null);
                    try
                    {
                        g_hash_table_insert(types, g_strdup("ref"), IntPtr.Zero); // SECRET_SCHEMA_ATTRIBUTE_STRING = 0.
                        Schema = secret_schema_newv("com.meadworks.beacon", 0, types);
                    }
                    finally { g_hash_table_unref(types); }
                    Attributes = g_hash_table_new_full(hash, equal, free, free);
                    if (reference != null) g_hash_table_insert(Attributes, g_strdup("ref"), g_strdup(reference));
                    Cancellable = g_cancellable_new();
                    if (Schema == IntPtr.Zero || Attributes == IntPtr.Zero || Cancellable == IntPtr.Zero) throw new KeyringUnavailableException();
                    registration = token.Register(() => g_cancellable_cancel(Cancellable));
                }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                // Dispose waits for a running managed cancellation callback before native unref.
                registration.Dispose();
                if (Cancellable != IntPtr.Zero) { g_object_unref(Cancellable); Cancellable = IntPtr.Zero; }
                if (Attributes != IntPtr.Zero) { g_hash_table_unref(Attributes); Attributes = IntPtr.Zero; }
                if (Schema != IntPtr.Zero) { secret_schema_unref(Schema); Schema = IntPtr.Zero; }
            }
        }

        [DllImport(Secret, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr secret_schema_newv([MarshalAs(UnmanagedType.LPUTF8Str)] string name, int flags, IntPtr types);
        [DllImport(Secret, CallingConvention = CallingConvention.Cdecl)] static extern void secret_schema_unref(IntPtr schema);
        [DllImport(Secret, CallingConvention = CallingConvention.Cdecl)] static extern int secret_password_storev_sync(IntPtr schema, IntPtr attrs, [MarshalAs(UnmanagedType.LPUTF8Str)] string collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string label, [MarshalAs(UnmanagedType.LPUTF8Str)] string password, IntPtr cancel, out IntPtr error);
        [DllImport(Secret, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr secret_password_lookupv_sync(IntPtr schema, IntPtr attrs, IntPtr cancel, out IntPtr error);
        [DllImport(Secret, CallingConvention = CallingConvention.Cdecl)] static extern int secret_password_clearv_sync(IntPtr schema, IntPtr attrs, IntPtr cancel, out IntPtr error);
        [DllImport(Secret, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr secret_password_searchv_sync(IntPtr schema, IntPtr attrs, int flags, IntPtr cancel, out IntPtr error);
        [DllImport(Secret, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr secret_retrievable_get_attributes(IntPtr item);
        [DllImport(Secret, CallingConvention = CallingConvention.Cdecl)] static extern void secret_password_free(IntPtr secret);
        [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr g_hash_table_new_full(Hash hash, Equal equal, Free keyFree, Free? valueFree);
        [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] static extern int g_hash_table_insert(IntPtr table, IntPtr key, IntPtr value);
        [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr g_hash_table_lookup(IntPtr table, [MarshalAs(UnmanagedType.LPUTF8Str)] string key);
        [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] static extern void g_hash_table_unref(IntPtr table);
        [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] static extern uint g_str_hash(IntPtr value);
        [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] static extern int g_str_equal(IntPtr a, IntPtr b);
        [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr g_strdup([MarshalAs(UnmanagedType.LPUTF8Str)] string value);
        [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] static extern void g_free(IntPtr value);
        [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] static extern void g_error_free(IntPtr error);
        [DllImport(Glib, CallingConvention = CallingConvention.Cdecl)] static extern void g_list_free(IntPtr list);
        [DllImport(Gio, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr g_cancellable_new();
        [DllImport(Gio, CallingConvention = CallingConvention.Cdecl)] static extern void g_cancellable_cancel(IntPtr value);
        [DllImport(Gobject, CallingConvention = CallingConvention.Cdecl)] static extern void g_object_unref(IntPtr value);
    }
}
