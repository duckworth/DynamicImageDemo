<%@ Control Language="C#" Inherits="System.Web.Mvc.ViewUserControl<DynamicImageDemo.Models.ImageModel>" %>
<img src='<%= Url.RouteUrl(new {
    controller = "Image", 
    action = "Show", 
    Model.Id,
    Model.Icon,
    Model.Text
})%>' />
