<%@ Page Language="C#" MasterPageFile="~/Views/Shared/Site.Master" Inherits="System.Web.Mvc.ViewPage<DynamicImageDemo.Models.DemoModel>" %>

<%@ Import Namespace="DynamicImageDemo.Models" %>
<asp:Content ID="Content1" ContentPlaceHolderID="TitleContent" runat="server">
    Dynamic PNG Demo
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
 
    <% using (Html.BeginForm()) {%>
    <%: Html.ValidationSummary(true) %>
    <fieldset>
        <legend>Settings</legend>
        <div class="editor-label">
            <%: Html.LabelFor(model => model.ForecastText) %>
        </div>
        <div class="editor-field">
            <%: Html.TextBoxFor(model => model.ForecastText) %>
            <%: Html.ValidationMessageFor(model => model.ForecastText) %>
        </div>
        <div class="editor-label">
            <%: Html.LabelFor(model => model.IconName) %>
        </div>
        <div class="editor-field">
            <%: Html.DropDownListFor(model => model.IconName, new SelectList(Model.Icons) ) %>
            <%: Html.ValidationMessageFor(model => model.IconName) %>
        </div>
        <p>
            <input type="submit" value="Generate" />
        </p>
    </fieldset>
    <% } %>
    <h1>
        Output Images</h1>
    <% foreach (var reducerType in Model.PngColorReducers)
       {%>
    <fieldset>
        <legend>
            <%:reducerType%></legend>
        <%
           Html.RenderPartial("DynamicImage",
                              new ImageModel
                                  {Icon = Model.IconName, Text = Model.ForecastText, Id = (int) reducerType});%>
    </fieldset>
    <%
       }%>
</asp:Content>
