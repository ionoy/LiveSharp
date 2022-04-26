package com.livesharp;

import javax.swing.*;

import com.intellij.uiDesigner.core.GridConstraints;
import com.intellij.uiDesigner.core.GridLayoutManager;
import com.livesharp.models.NewsItem;

import java.awt.*;
import java.io.IOException;
import java.net.URL;
import java.net.URLConnection;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.text.SimpleDateFormat;


public class NewsForm {

    private JPanel myPanel;
    private JTextPane textPane1;

    public NewsForm() {


        textPane1.setText("Loading...");
        //scrollPane1.setBorder(BorderFactory.createEmptyBorder());
        //scrollPane1.setViewportBorder(BorderFactory.createEmptyBorder());

    }

    //
    void getNewsAsync() {

    }

    void getNews() throws IOException {

        URL url = new URL("http://www.livexaml.com/news/json");
        URLConnection request = url.openConnection();
        request.connect();

        try {
            InputStreamReader reader = new InputStreamReader((InputStream) request.getContent());
            NewsItem[] newsList = (NewsItem[]) GsonUtil.deserializeListFromReader(reader, NewsItem[].class);

            if (newsList.length > 0) {

                StringBuffer listItemsHtml = new StringBuffer();
                for (NewsItem item : newsList) {
                    listItemsHtml.append("<li>"
                            + "<span class='date'>" + new SimpleDateFormat("MM-dd-yyyy").format(item.dateTime) + "</span><br>"
                            + "<span class='title'>" + item.title + "</span>"
                            + "<p class='content'>" + item.content + "</p></li>");
                }

                String headStyle = "<head><style type='text/css'> ul { list-style-type: none; font-family: Arial; margin:2px;} li { margin-bottom: 5px; } .title { color:#0ba7db; font-weight:bold; } .content { } .date { font-size: 9px; color: gray; }</style></head>";
                textPane1.setText("<html>" + headStyle
                        //+ "<image src='/icons/icon.png' height=40 width=40 />"
                        + "<ul>" + listItemsHtml + "</ul></html>");
                textPane1.setCaretPosition(0);
            } else {
                textPane1.setText("<html><p>No recent news to report</p></html>");
            }
        } catch (Exception ex) {
            textPane1.setText(ex.getCause().getLocalizedMessage());
        }
    }

    public JPanel getPanel() {

        try {
            getNews();
        } catch (IOException e) {
            e.printStackTrace();
        }

        return myPanel;
    }

    {
// GUI initializer generated by IntelliJ IDEA GUI Designer
// >>> IMPORTANT!! <<<
// DO NOT EDIT OR ADD ANY CODE HERE!
        $$$setupUI$$$();
    }

    /**
     * Method generated by IntelliJ IDEA GUI Designer
     * >>> IMPORTANT!! <<<
     * DO NOT edit this method OR call it in your code!
     *
     * @noinspection ALL
     */
    private void $$$setupUI$$$() {
        myPanel = new JPanel();
        myPanel.setLayout(new GridLayoutManager(1, 1, new Insets(0, 0, 0, 0), -1, -1));
        myPanel.setMaximumSize(new Dimension(600, 600));
        myPanel.setMinimumSize(new Dimension(600, 600));
        myPanel.setPreferredSize(new Dimension(600, 600));
        final JScrollPane scrollPane1 = new JScrollPane();
        myPanel.add(scrollPane1, new GridConstraints(0, 0, 1, 1, GridConstraints.ANCHOR_CENTER, GridConstraints.FILL_BOTH, GridConstraints.SIZEPOLICY_CAN_SHRINK | GridConstraints.SIZEPOLICY_WANT_GROW, GridConstraints.SIZEPOLICY_CAN_SHRINK | GridConstraints.SIZEPOLICY_WANT_GROW, null, null, new Dimension(600, 600), 0, false));
        textPane1 = new JTextPane();
        textPane1.setContentType("text/html");
        scrollPane1.setViewportView(textPane1);
    }

    /**
     * @noinspection ALL
     */
    public JComponent $$$getRootComponent$$$() {
        return myPanel;
    }
}


