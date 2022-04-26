package com.livesharp.models;

import java.util.Date;

public class LicenseResponseItem {

    public String AccessKey;
    public String LicenseKey;

    public String License;
    public String Signature;

    public Date ValidUntil;
    public Boolean IsValid;
    public Boolean IsMalformed;

    //public Boolean IsTrial -> ValidUntil != null && ValidUntil < new Date(System.currentTimeMillis().plusYears(1);
    public Boolean IsEmptyLicense;


}
