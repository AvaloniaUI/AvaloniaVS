#!/bin/bash
rm -rf list.txt
find ../..|grep vstemplate$|xargs cat|grep '<package '|while read pkg
do	
	name=`echo $pkg|sed 's/.*id=.//'|sed 's/".*//'`
	version=`echo $pkg| sed 's/.*version=.//'|sed 's/".*//'`
	file=$name.$version.nupkg
	echo $file
	curl -L -o $file "http://packages.nuget.org/api/v2/package/$name/$version"
	echo "<Asset Type=\"$file\" d:Source=\"File\" Path=\"packages\\$file\" d:VsixSubPath=\"packages\" />" >> list.txt
done