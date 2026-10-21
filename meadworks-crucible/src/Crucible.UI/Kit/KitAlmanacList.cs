// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// AX11: ordered draft rows over the existing virtual list; only Apply submits to the owner.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public static class KitAlmanacList
    {
        sealed class Item{internal int Id;internal string Value;}
        public static void Open(KitWindow parent,string title,IReadOnlyList<string> initial,Func<string,string> validate,Action<IReadOnlyList<string>> apply,
            Func<KitWindow,RectTransform,string,Action<string>,Action> editor=null)
        {
            var dialog=KitAlmanacDialog.Create(parent,title,new Vector2(700,520));if(dialog==null)return;
            int next=0,revision=0;var items=initial.Select(value=>new Item{Id=next++,Value=value}).ToList();
            var owned=new Dictionary<RectTransform,Action>();
            var list=KitVirtualList.Create(dialog.Panel,"Items",new Vector2(660,360),p=>{var go=new GameObject("Item",typeof(RectTransform));go.transform.SetParent(p,false);return (RectTransform)go.transform;});
            KitAlmanacDialog.Place(list.Root,new Vector2(20,-55),new Vector2(660,360));dialog.Own(list.Dispose);
            dialog.Tick+=list.Tick;
            var error=KitAlmanacDialog.Label(dialog.Panel,"Validation","",new Vector2(20,-420),new Vector2(650,30),14);
            void Unbind(RectTransform rect)
            {
                foreach(var control in rect.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))dialog.Window.Unfocusable(control);
                if(owned.TryGetValue(rect,out var release)){release();owned.Remove(rect);}
                foreach(Transform child in rect){child.gameObject.SetActive(false);child.name="retired";UnityEngine.Object.Destroy(child.gameObject);}
            }
            void Refresh()
            {
                var rows=new List<KitVirtualRow>();
                foreach(var item in items)
                {
                    rows.Add(new KitVirtualRow(item.Id.ToString(),44,rect=>
                    {
                        Unbind(rect);
                        if(editor!=null)owned[rect]=editor(dialog.Window,rect,item.Value,value=>item.Value=value);
                        else
                        {
                            if(item.Value.Length>KitInputLayout.TextLength||item.Value.IndexOfAny(new[]{'\n','\r','\t'})>=0)
                                KitButton.Create(rect,"Value",KitText.Text(KitText.EditText,CrucibleUiPlugin.Language),()=>KitAlmanacTextEditor.Open(dialog.Window,"Item",item.Value,validate,text=>item.Value=text),Vector2.zero,new Vector2(410,32));
                            else
                            {var field=KitTextField.Create(rect,"Value",KitFieldMode.Text,Vector2.zero,new Vector2(410,32));field.Text=item.Value;field.Validator=text=>string.IsNullOrEmpty(validate(text));field.Ended+=edit=>{if(edit.Commit)item.Value=edit.Text;};owned[rect]=field.Destroy;}
                        }
                        KitButton.Create(rect,"Up","↑",()=>Move(item,-1),new Vector2(420,0),new Vector2(48,32));
                        KitButton.Create(rect,"Down","↓",()=>Move(item,1),new Vector2(475,0),new Vector2(48,32));
                        KitButton.Create(rect,"Remove","Remove",()=>{items.Remove(item);Refresh();},new Vector2(530,0),new Vector2(100,32));
                        int y=items.IndexOf(item);int x=0;foreach(var control in rect.GetComponentsInChildren<UnityEngine.UI.Selectable>())dialog.Window.Focusable(control,x++,y);
                    },Unbind));
                }
                list.SetRows(rows,++revision);
            }
            void Move(Item item,int delta){int at=items.IndexOf(item),to=Mathf.Clamp(at+delta,0,items.Count-1);if(at==to)return;items.RemoveAt(at);items.Insert(to,item);Refresh();}
            KitButton.Create(dialog.Panel,"Add","Add",()=>{items.Add(new Item{Id=next++,Value=""});Refresh();},new Vector2(20,-470),new Vector2(90,32));
            var save=KitButton.Create(dialog.Panel,"Apply","Apply",()=>
            {
                error.text=items.Select(item=>validate(item.Value)).FirstOrDefault(message=>!string.IsNullOrEmpty(message))??"";
                if(error.text.Length==0){apply(items.Select(item=>item.Value).ToArray());dialog.Dispose();}
            },new Vector2(460,-470),new Vector2(100,32));
            var cancel=KitButton.Create(dialog.Panel,"Cancel","Cancel",dialog.Dispose,new Vector2(575,-470),new Vector2(105,32));dialog.Window.Focusable(save,0,items.Count+1);dialog.Window.Focusable(cancel,1,items.Count+1);Refresh();
        }
    }
}
