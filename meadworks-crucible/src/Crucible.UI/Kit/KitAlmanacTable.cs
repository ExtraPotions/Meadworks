// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Multi-column draft rows over the existing virtual list, beside KitAlmanacList; only Apply submits to the owner.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static class KitAlmanacTable
    {
        sealed class Row{internal int Id;internal string[] Cells;}
        const float Width=720,RemoveWidth=100,Gap=6;

        /// <summary>
        /// Edits rows of equal width. <paramref name="cellError"/> checks one cell (empty means valid) and
        /// <paramref name="rowError"/> a whole row before Apply. <paramref name="alternate"/> adds one button (for example a
        /// switch to the plain list) that receives the current draft rows; the dialog closes first.
        /// </summary>
        public static KitAlmanacDialog Open(KitWindow parent,string title,IReadOnlyList<string> headers,IReadOnlyList<string[]> initial,
            Func<string,string> cellError,Func<string[],string> rowError,Action<IReadOnlyList<string[]>> apply,
            string addLabel="Add",string removeLabel="Remove",string alternateLabel=null,Action<IReadOnlyList<string[]>> alternate=null)
        {
            int columns=headers.Count;if(columns<1)return null;
            var dialog=KitAlmanacDialog.Create(parent,title,new Vector2(Width+40,560));if(dialog==null)return null;
            int next=0,revision=0;var rows=initial.Select(cells=>new Row{Id=next++,Cells=Fit(cells,columns)}).ToList();
            float cell=(Width-RemoveWidth-Gap*columns)/columns;
            for(int c=0;c<columns;c++)KitAlmanacDialog.Label(dialog.Panel,"Header"+c,headers[c],new Vector2(20+c*(cell+Gap),-50),new Vector2(cell,26),14);
            var owned=new Dictionary<RectTransform,List<Action>>();
            var list=KitVirtualList.Create(dialog.Panel,"Rows",new Vector2(Width,350),p=>{var go=new GameObject("Row",typeof(RectTransform));go.transform.SetParent(p,false);return (RectTransform)go.transform;});
            KitAlmanacDialog.Place(list.Root,new Vector2(20,-80),new Vector2(Width,350));dialog.Own(list.Dispose);dialog.Tick+=list.Tick;
            var error=KitAlmanacDialog.Label(dialog.Panel,"Validation","",new Vector2(20,-438),new Vector2(Width,30),14);
            void Unbind(RectTransform rect)
            {
                foreach(var control in rect.GetComponentsInChildren<Selectable>(true))dialog.Window.Unfocusable(control);
                if(owned.TryGetValue(rect,out var release)){foreach(var r in release)r();owned.Remove(rect);}
                foreach(Transform child in rect){child.gameObject.SetActive(false);child.name="retired";UnityEngine.Object.Destroy(child.gameObject);}
            }
            void Refresh()
            {
                var virtualRows=new List<KitVirtualRow>();
                foreach(var row in rows)
                {
                    virtualRows.Add(new KitVirtualRow(row.Id.ToString(),40,rect=>
                    {
                        Unbind(rect);var release=new List<Action>();owned[rect]=release;
                        for(int c=0;c<columns;c++)
                        {
                            int column=c;
                            var field=KitTextField.Create(rect,"Cell"+c,KitFieldMode.Text,new Vector2(c*(cell+Gap),0),new Vector2(cell,32));
                            field.Text=row.Cells[c];field.Validator=text=>string.IsNullOrEmpty(cellError?.Invoke(text));
                            field.Changed+=text=>{row.Cells[column]=text;error.text=cellError?.Invoke(text)??"";};
                            release.Add(field.Destroy);
                        }
                        KitButton.Create(rect,"Remove",removeLabel,()=>{rows.Remove(row);Refresh();},new Vector2(Width-RemoveWidth,0),new Vector2(RemoveWidth,32));
                        int y=rows.IndexOf(row),x=0;foreach(var control in rect.GetComponentsInChildren<Selectable>())dialog.Window.Focusable(control,x++,y);
                    },Unbind));
                }
                list.SetRows(virtualRows,++revision);
            }
            string Problem()
            {
                foreach(var row in rows)
                {
                    foreach(var value in row.Cells){var e=cellError?.Invoke(value);if(!string.IsNullOrEmpty(e))return e;}
                    var r=rowError?.Invoke(row.Cells);if(!string.IsNullOrEmpty(r))return r;
                }
                return "";
            }
            IReadOnlyList<string[]> Draft()=>rows.Select(row=>(string[])row.Cells.Clone()).ToArray();
            int y0=rows.Count+1;
            var add=KitButton.Create(dialog.Panel,"Add",addLabel,()=>{rows.Add(new Row{Id=next++,Cells=Fit(Array.Empty<string>(),columns)});Refresh();},new Vector2(20,-480),new Vector2(90,32));
            Button other=null;
            if(alternate!=null&&!string.IsNullOrEmpty(alternateLabel))
                other=KitButton.Create(dialog.Panel,"Alternate",alternateLabel,()=>{var draft=Draft();dialog.Dispose();alternate(draft);},new Vector2(120,-480),new Vector2(140,32));
            var save=KitButton.Create(dialog.Panel,"Apply","Apply",()=>
            {
                error.text=Problem();
                if(error.text.Length==0){apply(Draft());dialog.Dispose();}
            },new Vector2(Width-205,-480),new Vector2(100,32));
            var cancel=KitButton.Create(dialog.Panel,"Cancel","Cancel",dialog.Dispose,new Vector2(Width-95,-480),new Vector2(105,32));
            dialog.Window.Focusable(add,0,y0);if(other!=null)dialog.Window.Focusable(other,1,y0);dialog.Window.Focusable(save,2,y0);dialog.Window.Focusable(cancel,3,y0);
            Refresh();return dialog;
        }

        static string[] Fit(IReadOnlyList<string> cells,int columns){var fitted=new string[columns];for(int c=0;c<columns;c++)fitted[c]=c<cells.Count?cells[c]??"":"";return fitted;}
    }
}
