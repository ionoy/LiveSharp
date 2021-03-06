package com.livesharp;

import com.intellij.uiDesigner.core.GridConstraints;
import com.intellij.uiDesigner.core.GridLayoutManager;

import javax.swing.*;
import java.awt.*;
import java.net.URI;


public class ExpirationForm {

    private JTextPane trialPeriodHasExpiredTextPane;
    private JTextField textField1;
    private JTextPane pleaseProvideAnEmailTextPane2;
    private JPanel myPanel;
    private JButton btnPurchase;

    ExpirationForm() {

        btnPurchase.addActionListener(e -> {
            Subscribe();
        });
    }

    private void Subscribe() {
        String val = textField1.getText();
        if (val.isEmpty()) {
            JOptionPane.showMessageDialog(null, "Please enter an email", "Missing Email", JOptionPane.INFORMATION_MESSAGE);
            return;
        }

        String url = "http://www.livexaml.com?subscribe=" + val;

        try {
            if (Desktop.isDesktopSupported() && Desktop.getDesktop().isSupported(Desktop.Action.BROWSE)) {
                Desktop.getDesktop().browse(new URI(url));
            }
        } catch (Exception ex) {

        }
    }

    JPanel getPanel() {
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
        myPanel.setLayout(new GridLayoutManager(8, 2, new Insets(0, 0, 0, 0), -1, -1));
        final JLabel label1 = new JLabel();
        Font label1Font = this.$$$getFont$$$(null, -1, 22, label1.getFont());
        if (label1Font != null) label1.setFont(label1Font);
        label1.setIcon(new ImageIcon(getClass().getResource("/icons/icon_64.png")));
        label1.setText("LiveSharp");
        myPanel.add(label1, new GridConstraints(0, 0, 1, 2, GridConstraints.ANCHOR_CENTER, GridConstraints.FILL_NONE, GridConstraints.SIZEPOLICY_FIXED, GridConstraints.SIZEPOLICY_FIXED, null, null, null, 0, false));
        trialPeriodHasExpiredTextPane = new JTextPane();
        trialPeriodHasExpiredTextPane.setEditable(false);
        Font trialPeriodHasExpiredTextPaneFont = this.$$$getFont$$$(null, -1, 16, trialPeriodHasExpiredTextPane.getFont());
        if (trialPeriodHasExpiredTextPaneFont != null)
            trialPeriodHasExpiredTextPane.setFont(trialPeriodHasExpiredTextPaneFont);
        trialPeriodHasExpiredTextPane.setOpaque(false);
        trialPeriodHasExpiredTextPane.setText("Trial period has expired. LiveSharp will be limited to projects with no more than 3 XAML files.  Please consider purchasing the license. By purchasing, you also support free products like Ammy XAML language.");
        myPanel.add(trialPeriodHasExpiredTextPane, new GridConstraints(1, 0, 7, 1, GridConstraints.ANCHOR_CENTER, GridConstraints.FILL_VERTICAL, GridConstraints.SIZEPOLICY_CAN_SHRINK | GridConstraints.SIZEPOLICY_WANT_GROW, GridConstraints.SIZEPOLICY_WANT_GROW, null, new Dimension(300, -1), null, 1, false));
        final JLabel label2 = new JLabel();
        Font label2Font = this.$$$getFont$$$(null, -1, 14, label2.getFont());
        if (label2Font != null) label2.setFont(label2Font);
        label2.setText("Subscribe for $7 per month");
        myPanel.add(label2, new GridConstraints(6, 1, 1, 1, GridConstraints.ANCHOR_CENTER, GridConstraints.FILL_NONE, GridConstraints.SIZEPOLICY_FIXED, GridConstraints.SIZEPOLICY_FIXED, null, null, null, 0, false));
        textField1 = new JTextField();
        myPanel.add(textField1, new GridConstraints(4, 1, 1, 1, GridConstraints.ANCHOR_WEST, GridConstraints.FILL_HORIZONTAL, GridConstraints.SIZEPOLICY_WANT_GROW, GridConstraints.SIZEPOLICY_FIXED, null, new Dimension(150, -1), null, 0, false));
        final JLabel label3 = new JLabel();
        label3.setIcon(new ImageIcon(getClass().getResource("/icons/paypal.png")));
        label3.setText("");
        myPanel.add(label3, new GridConstraints(1, 1, 1, 1, GridConstraints.ANCHOR_CENTER, GridConstraints.FILL_NONE, GridConstraints.SIZEPOLICY_FIXED, GridConstraints.SIZEPOLICY_FIXED, null, null, null, 0, false));
        btnPurchase = new JButton();
        Font btnPurchaseFont = this.$$$getFont$$$(null, -1, 16, btnPurchase.getFont());
        if (btnPurchaseFont != null) btnPurchase.setFont(btnPurchaseFont);
        btnPurchase.setMargin(new Insets(0, 0, 2, 0));
        btnPurchase.setText("Purchase");
        myPanel.add(btnPurchase, new GridConstraints(5, 1, 1, 1, GridConstraints.ANCHOR_CENTER, GridConstraints.FILL_NONE, GridConstraints.SIZEPOLICY_CAN_SHRINK | GridConstraints.SIZEPOLICY_CAN_GROW, GridConstraints.SIZEPOLICY_FIXED, null, null, null, 0, false));
        pleaseProvideAnEmailTextPane2 = new JTextPane();
        pleaseProvideAnEmailTextPane2.setEditable(false);
        Font pleaseProvideAnEmailTextPane2Font = this.$$$getFont$$$(null, -1, 16, pleaseProvideAnEmailTextPane2.getFont());
        if (pleaseProvideAnEmailTextPane2Font != null)
            pleaseProvideAnEmailTextPane2.setFont(pleaseProvideAnEmailTextPane2Font);
        pleaseProvideAnEmailTextPane2.setOpaque(false);
        pleaseProvideAnEmailTextPane2.setText("Please provide an email that will be later used to activate your license:");
        myPanel.add(pleaseProvideAnEmailTextPane2, new GridConstraints(3, 1, 1, 1, GridConstraints.ANCHOR_SOUTH, GridConstraints.FILL_HORIZONTAL, GridConstraints.SIZEPOLICY_WANT_GROW, GridConstraints.SIZEPOLICY_WANT_GROW, null, new Dimension(200, 30), null, 0, false));
    }

    /**
     * @noinspection ALL
     */
    private Font $$$getFont$$$(String fontName, int style, int size, Font currentFont) {
        if (currentFont == null) return null;
        String resultName;
        if (fontName == null) {
            resultName = currentFont.getName();
        } else {
            Font testFont = new Font(fontName, Font.PLAIN, 10);
            if (testFont.canDisplay('a') && testFont.canDisplay('1')) {
                resultName = fontName;
            } else {
                resultName = currentFont.getName();
            }
        }
        return new Font(resultName, style >= 0 ? style : currentFont.getStyle(), size >= 0 ? size : currentFont.getSize());
    }

    /**
     * @noinspection ALL
     */
    public JComponent $$$getRootComponent$$$() {
        return myPanel;
    }
}
