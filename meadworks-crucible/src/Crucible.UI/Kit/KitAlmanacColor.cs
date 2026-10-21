// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// AX09 uses Unity's native HSV conversion and procedural texture, composed with kit fields/buttons.
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public sealed class KitAlmanacColor : MonoBehaviour,IPointerDownHandler,IDragHandler
    {
        KitAlmanacDialog dialog;RawImage square;Image preview;RectTransform marker;
        Texture2D texture,hueTexture;Color value;float hue,saturation,brightness;
        KitAlmanacNumber hueField;KitTextField hex;readonly KitAlmanacNumber[] components=new KitAlmanacNumber[4];
        bool changing,cancelled;Action<Color> commit;
        public static void Open(KitWindow parent,string title,Color initial,Action<Color> commit)
        {
            var dialog=KitAlmanacDialog.Create(parent,title,new Vector2(620,580));if(dialog==null)return;
            var node=new GameObject("SaturationValue",typeof(RectTransform),typeof(RawImage));node.transform.SetParent(dialog.Panel,false);
            KitAlmanacDialog.Place((RectTransform)node.transform,new Vector2(20,-60),new Vector2(235,235));
            var picker=node.AddComponent<KitAlmanacColor>();picker.dialog=dialog;picker.commit=commit;picker.square=node.GetComponent<RawImage>();picker.value=initial;
            var swatch=new GameObject("Preview",typeof(RectTransform),typeof(Image));swatch.transform.SetParent(dialog.Panel,false);
            picker.preview=swatch.GetComponent<Image>();KitAlmanacDialog.Place(picker.preview.rectTransform,new Vector2(20,-310),new Vector2(235,32));
            var mark=new GameObject("Selection",typeof(RectTransform),typeof(Image));mark.transform.SetParent(node.transform,false);mark.GetComponent<Image>().sprite=KitArt.Button;mark.GetComponent<Image>().raycastTarget=false;
            picker.marker=(RectTransform)mark.transform;picker.marker.sizeDelta=new Vector2(10,10);
            KitAppearance.Changed+=picker.Restyle;
            string[] labels={"R","G","B","A"};
            for(int i=0;i<4;i++)
            {
                int component=i;
                KitAlmanacDialog.Label(dialog.Panel,"Label"+i,labels[i],new Vector2(280,-60-i*72),new Vector2(24,28));
                picker.components[i]=KitAlmanacNumber.Create(dialog.Panel,"Component"+i,new Vector2(310,-60-i*72),new Vector2(280,32),"0",Valid,
                    text=>{if(float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)){picker.value[component]=Mathf.Clamp01(n);picker.Follow(true);}},
                    direction=>(picker.value[component]+direction*.01f).ToString("R",CultureInfo.InvariantCulture),0,1,.01);
                dialog.Own(picker.components[i].Dispose);dialog.Window.Focusable(picker.components[i].Field.Input,1,i);
            }
            picker.hueField=KitAlmanacNumber.Create(dialog.Panel,"Hue",new Vector2(310,-348),new Vector2(280,65),"0",Valid,
                text=>{if(float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var h)){picker.hue=Mathf.Repeat(h/360f,1);picker.FromHsv();}},
                direction=>(picker.hue*360+direction).ToString("R",CultureInfo.InvariantCulture),0,360,1);
            dialog.Own(picker.hueField.Dispose);
            picker.MakeHueStrip();
            picker.hex=KitTextField.Create(dialog.Panel,"Hex",KitFieldMode.Text,new Vector2(310,-425),new Vector2(280,32));picker.hex.Validator=text=>ColorUtility.TryParseHtmlString("#"+text,out _);
            picker.hex.Ended+=edit=>{if(edit.Commit&&ColorUtility.TryParseHtmlString("#"+edit.Text,out var colour)){picker.value=colour;picker.Follow(true);}};dialog.Own(picker.hex.Destroy);
            var apply=KitButton.Create(dialog.Panel,"Apply","Apply",dialog.Dispose,new Vector2(400,-530),new Vector2(90,30));
            var cancel=KitButton.Create(dialog.Panel,"Cancel","Cancel",()=>{picker.cancelled=true;dialog.Dispose();},new Vector2(500,-530),new Vector2(90,30));
            dialog.Window.Focusable(apply,0,5);dialog.Window.Focusable(cancel,1,5);
            dialog.Closed+=()=>{if(!picker.cancelled)picker.commit(picker.value);};
            picker.Follow(true);
        }
        static bool Valid(string value)=>float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&!float.IsNaN(n)&&!float.IsInfinity(n);
        void MakeHueStrip()
        {
            var slider=hueField.Slider;if(slider==null)return;
            slider.direction=Slider.Direction.LeftToRight;
            hueTexture=new Texture2D(256,1,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            for(int x=0;x<256;x++)hueTexture.SetPixel(x,0,Color.HSVToRGB(x/255f,1,1));hueTexture.Apply();
            var go=new GameObject("HueGradient",typeof(RectTransform),typeof(RawImage));go.layer=slider.gameObject.layer;go.transform.SetParent(slider.transform,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var strip=go.GetComponent<RawImage>();strip.texture=hueTexture;strip.raycastTarget=false;
            // Keep the existing native slider handle and pointer/keyboard behavior above the gradient.
            if(slider.handleRect!=null)
            {
                Transform layer=slider.handleRect;
                while(layer.parent!=null&&layer.parent!=slider.transform)layer=layer.parent;
                layer.SetAsLastSibling();
            }
        }
        void Follow(bool convert)
        {
            if(changing)return;changing=true;
            if(convert)Color.RGBToHSV(value,out hue,out saturation,out brightness);
            if(texture==null)texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)texture.SetPixel(x,y,Color.HSVToRGB(hue,x/63f,y/63f));texture.Apply();square.texture=texture;
            preview.color=value;marker.anchorMin=marker.anchorMax=new Vector2(saturation,brightness);marker.anchoredPosition=Vector2.zero;
            for(int i=0;i<4;i++)components[i].Set(value[i].ToString("R",CultureInfo.InvariantCulture));
            hueField.Set((hue*360).ToString("R",CultureInfo.InvariantCulture));if(!hex.Focused)hex.Text=ColorUtility.ToHtmlStringRGBA(value);
            changing=false;
        }
        void FromHsv(){float alpha=value.a;value=Color.HSVToRGB(hue,saturation,brightness);value.a=alpha;Follow(false);}
        void Point(PointerEventData data)
        {
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(square.rectTransform,data.position,data.pressEventCamera,out var point))
            {var rect=square.rectTransform.rect;saturation=Mathf.Clamp01((point.x-rect.xMin)/rect.width);brightness=Mathf.Clamp01((point.y-rect.yMin)/rect.height);FromHsv();}
        }
        public void OnPointerDown(PointerEventData data)=>Point(data);
        public void OnDrag(PointerEventData data)=>Point(data);
        // The marker's button art follows the Timber/Brown look while the picker stays open.
        void Restyle(){if(marker!=null)marker.GetComponent<Image>().sprite=KitArt.Button;}
        void OnDestroy(){KitAppearance.Changed-=Restyle;if(texture!=null)Destroy(texture);if(hueTexture!=null)Destroy(hueTexture);}
    }
}
